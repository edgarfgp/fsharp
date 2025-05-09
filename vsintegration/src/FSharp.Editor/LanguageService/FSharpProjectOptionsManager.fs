﻿// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.Collections.Immutable
open System.IO
open System.Linq
open Microsoft.CodeAnalysis
open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open Microsoft.VisualStudio.FSharp.Editor
open System.Threading
open Microsoft.VisualStudio.FSharp.Interactive.Session
open System.Runtime.CompilerServices
open CancellableTasks
open Microsoft.VisualStudio.FSharp.Editor.Extensions
open System.Windows
open Microsoft.VisualStudio
open FSharp.Compiler.Text
open Microsoft.VisualStudio.TextManager.Interop

#nowarn "57"

[<AutoOpen>]
module private FSharpProjectOptionsHelpers =

    let mapCpsProjectToSite (project: Project, cpsCommandLineOptions: IDictionary<ProjectId, string[] * string[]>) =
        let sourcePaths, referencePaths, options =
            match cpsCommandLineOptions.TryGetValue(project.Id) with
            | true, (sourcePaths, options) -> sourcePaths, [||], options
            | false, _ -> [||], [||], [||]

        let mutable errorReporter = Unchecked.defaultof<_>

        { new IProjectSite with
            member _.Description = project.Name
            member _.CompilationSourceFiles = sourcePaths

            member _.CompilationOptions =
                Array.concat [ options; referencePaths |> Array.map (fun r -> "-r:" + r) ]

            member _.CompilationReferences = referencePaths

            member site.CompilationBinOutputPath =
                site.CompilationOptions
                |> Array.tryPick (fun s -> if s.StartsWith("-o:") then Some s.[3..] else None)

            member _.ProjectFileName = project.FilePath
            member _.AdviseProjectSiteChanges(_, _) = ()
            member _.AdviseProjectSiteCleaned(_, _) = ()
            member _.AdviseProjectSiteClosed(_, _) = ()
            member _.IsIncompleteTypeCheckEnvironment = false
            member _.TargetFrameworkMoniker = ""
            member _.ProjectGuid = project.Id.Id.ToString()
            member _.LoadTime = System.DateTime.Now
            member _.ProjectProvider = None

            member _.BuildErrorReporter
                with get () = errorReporter
                and set (v) = errorReporter <- v
        }

    let inline hasProjectVersionChanged (oldProject: Project) (newProject: Project) =
        oldProject.Version <> newProject.Version

    let hasDependentVersionChanged (oldProject: Project) (newProject: Project) (ct: CancellationToken) =
        let oldProjectMetadataRefs = oldProject.MetadataReferences
        let newProjectMetadataRefs = newProject.MetadataReferences

        if oldProjectMetadataRefs.Count <> newProjectMetadataRefs.Count then
            true
        else

            let oldProjectRefs = oldProject.ProjectReferences
            let newProjectRefs = newProject.ProjectReferences

            oldProjectRefs.Count() <> newProjectRefs.Count()
            || (oldProjectRefs, newProjectRefs)
               ||> Seq.exists2 (fun p1 p2 ->
                   ct.ThrowIfCancellationRequested()
                   let doesProjectIdDiffer = p1.ProjectId <> p2.ProjectId
                   let p1 = oldProject.Solution.GetProject(p1.ProjectId)
                   let p2 = newProject.Solution.GetProject(p2.ProjectId)

                   doesProjectIdDiffer
                   || (if p1.IsFSharp then
                           p1.Version <> p2.Version
                       else
                           let v1 = p1.GetDependentVersionAsync(ct).Result
                           let v2 = p2.GetDependentVersionAsync(ct).Result
                           v1 <> v2))

    let isProjectInvalidated (oldProject: Project) (newProject: Project) ct =
        let hasProjectVersionChanged = hasProjectVersionChanged oldProject newProject

        if newProject.AreFSharpInMemoryCrossProjectReferencesEnabled then
            hasProjectVersionChanged || hasDependentVersionChanged oldProject newProject ct
        else
            hasProjectVersionChanged

[<RequireQualifiedAccess>]
type private FSharpProjectOptionsMessage =
    | TryGetOptionsByDocument of
        Document *
        AsyncReplyChannel<(FSharpParsingOptions * FSharpProjectOptions) voption> *
        CancellationToken *
        userOpName: string
    | TryGetOptionsByProject of Project * AsyncReplyChannel<(FSharpParsingOptions * FSharpProjectOptions) voption> * CancellationToken
    | ClearOptions of ProjectId
    | ClearSingleFileOptionsCache of DocumentId

[<Sealed>]
type private FSharpProjectOptionsReactor(checker: FSharpChecker) =
    let cancellationTokenSource = new CancellationTokenSource()

    // Store command line options
    let commandLineOptions = ConcurrentDictionary<ProjectId, string[] * string[]>()

    let legacyProjectSites = ConcurrentDictionary<ProjectId, IProjectSite>()

    let cache =
        ConcurrentDictionary<ProjectId, Project * FSharpParsingOptions * FSharpProjectOptions>()

    let singleFileCache =
        ConcurrentDictionary<DocumentId, Project * VersionStamp * FSharpParsingOptions * FSharpProjectOptions * ConnectionPointSubscription>()

    // This is used to not constantly emit the same compilation.
    let weakPEReferences = ConditionalWeakTable<Compilation, FSharpReferencedProject>()
    let lastSuccessfulCompilations = ConcurrentDictionary<ProjectId, Compilation>()

    let scriptUpdatedEvent = Event<FSharpProjectOptions>()

    let createPEReference (referencedProject: Project) (comp: Compilation) =
        let projectId = referencedProject.Id

        match weakPEReferences.TryGetValue comp with
        | true, fsRefProj -> fsRefProj
        | _ ->
            let mutable strongComp = comp
            let weakComp = WeakReference<Compilation>(comp)
            let mutable stamp = DateTime.UtcNow

            // Getting a C# reference assembly can fail if there are compilation errors that cannot be resolved.
            // To mitigate this, we store the last successful compilation of a C# project and re-use it until we get a new successful compilation.
            let getStream =
                fun ct ->
                    let tryStream (comp: Compilation) =
                        let ms = new MemoryStream() // do not dispose the stream as it will be owned on the reference.

                        let emitOptions =
                            Emit.EmitOptions(metadataOnly = true, includePrivateMembers = false, tolerateErrors = true)

                        try
                            let result = comp.Emit(ms, options = emitOptions, cancellationToken = ct)

                            if result.Success then
                                strongComp <- Unchecked.defaultof<_> // Stop strongly holding the compilation since we have a result.
                                lastSuccessfulCompilations.[projectId] <- comp
                                ms.Position <- 0L
                                ms :> Stream |> Some
                            else
                                strongComp <- Unchecked.defaultof<_> // Stop strongly holding the compilation since we have a result.
                                ms.Dispose() // it failed, dispose of stream
                                None
                        with
                        | :? OperationCanceledException ->
                            // Since we cancelled, do not null out the strong compilation ref and update the stamp.
                            stamp <- DateTime.UtcNow
                            ms.Dispose()
                            None
                        | _ ->
                            strongComp <- Unchecked.defaultof<_> // Stop strongly holding the compilation since we have a result.
                            ms.Dispose() // it failed, dispose of stream
                            None

                    let resultOpt =
                        match weakComp.TryGetTarget() with
                        | true, comp -> tryStream comp
                        | _ -> None

                    match resultOpt with
                    | Some _ -> resultOpt
                    | _ ->
                        match lastSuccessfulCompilations.TryGetValue(projectId) with
                        | true, comp -> tryStream comp
                        | _ -> None

            let getStamp = fun () -> stamp

            let fsRefProj =
                FSharpReferencedProject.PEReference(getStamp, DelayedILModuleReader(referencedProject.OutputFilePath, getStream))

            weakPEReferences.Add(comp, fsRefProj)
            fsRefProj

    let rec tryComputeOptionsBySingleScriptOrFile (document: Document) userOpName =
        cancellableTask {
            let! ct = CancellableTask.getCancellationToken ()
            let! fileStamp = document.GetTextVersionAsync(ct)
            let textViewAndCaret () : (IVsTextView * Position) option = document.TryGetTextViewAndCaretPos()

            match singleFileCache.TryGetValue(document.Id) with
            | false, _ ->
                let! sourceText = document.GetTextAsync(ct)

                let getProjectOptionsFromScript textViewAndCaret =
                    let caret = textViewAndCaret ()

                    match caret with
                    | None ->
                        checker.GetProjectOptionsFromScript(
                            document.FilePath,
                            sourceText.ToFSharpSourceText(),
                            previewEnabled = SessionsProperties.fsiPreview,
                            assumeDotNetFramework = not SessionsProperties.fsiUseNetCore,
                            userOpName = userOpName
                        )

                    | Some(_, caret) ->
                        checker.GetProjectOptionsFromScript(
                            document.FilePath,
                            sourceText.ToFSharpSourceText(),
                            caret,
                            previewEnabled = SessionsProperties.fsiPreview,
                            assumeDotNetFramework = not SessionsProperties.fsiUseNetCore,
                            userOpName = userOpName
                        )

                let! scriptProjectOptions, _ = getProjectOptionsFromScript textViewAndCaret
                let project = document.Project

                let otherOptions =
                    if project.IsFSharpMetadata then
                        project.ProjectReferences
                        |> Seq.map (fun x -> "-r:" + project.Solution.GetProject(x.ProjectId).OutputFilePath)
                        |> Array.ofSeq
                        |> Array.append (
                            project.MetadataReferences.OfType<PortableExecutableReference>()
                            |> Seq.map (fun x -> "-r:" + x.FilePath)
                            |> Array.ofSeq
                        )
                    else
                        [||]

                let projectOptions =
                    if isScriptFile document.FilePath then
                        scriptUpdatedEvent.Trigger(scriptProjectOptions)
                        scriptProjectOptions
                    else
                        {
                            ProjectFileName = document.FilePath
                            ProjectId = None
                            SourceFiles = [| document.FilePath |]
                            OtherOptions = otherOptions
                            ReferencedProjects = [||]
                            IsIncompleteTypeCheckEnvironment = false
                            UseScriptResolutionRules = CompilerEnvironment.MustBeSingleFileProject(Path.GetFileName(document.FilePath))
                            LoadTime = DateTime.Now
                            UnresolvedReferences = None
                            OriginalLoadReferences = []
                            Stamp = Some(int64 (fileStamp.GetHashCode()))
                        }

                let parsingOptions, _ = checker.GetParsingOptionsFromProjectOptions(projectOptions)

                let updateProjectOptions () =
                    async {
                        let! scriptProjectOptions, _ = getProjectOptionsFromScript textViewAndCaret

                        checker.NotifyFileChanged(document.FilePath, scriptProjectOptions)
                        |> Async.Start
                    }
                    |> Async.Start

                let onChangeCaretHandler (_, _newline: int, _oldline: int) = updateProjectOptions ()
                let onKillFocus (_) = updateProjectOptions ()
                let onSetFocus (_) = updateProjectOptions ()

                let addToCacheAndSubscribe value =
                    match value with
                    | projectId, fileStamp, parsingOptions, projectOptions, _ ->
                        let subscription =
                            match textViewAndCaret () with
                            | Some(textView, _) ->
                                subscribeToTextViewEvents (textView, (Some onChangeCaretHandler), (Some onKillFocus), (Some onSetFocus))
                            | None -> None

                        (projectId, fileStamp, parsingOptions, projectOptions, subscription)

                singleFileCache.AddOrUpdate(
                    document.Id, // The key to the cache
                    (fun _ value -> addToCacheAndSubscribe value), // Function to add the cached value if the key does not exist
                    (fun _ _ value -> value), // Function to update the value if the key exists
                    (document.Project, fileStamp, parsingOptions, projectOptions, None) // The value to add or update
                )
                |> ignore

                return ValueSome(parsingOptions, projectOptions)

            | true, (oldProject, oldFileStamp, parsingOptions, projectOptions, _) ->
                if fileStamp <> oldFileStamp || isProjectInvalidated document.Project oldProject ct then
                    match singleFileCache.TryRemove(document.Id) with
                    | true, (_, _, _, _, Some subscription) -> subscription.Dispose()
                    | _ -> ()

                    return! tryComputeOptionsBySingleScriptOrFile document userOpName
                else
                    return ValueSome(parsingOptions, projectOptions)
        }

    let tryGetProjectSite (project: Project) =
        // Cps
        if commandLineOptions.ContainsKey project.Id then
            ValueSome(mapCpsProjectToSite (project, commandLineOptions))
        else
            // Legacy
            match legacyProjectSites.TryGetValue project.Id with
            | true, site -> ValueSome site
            | _ -> ValueNone

    let rec tryComputeOptions (project: Project) =
        cancellableTask {
            let projectId = project.Id
            let! ct = CancellableTask.getCancellationToken ()

            match cache.TryGetValue(projectId) with
            | false, _ ->

                // Because this code can be kicked off before the hack, HandleCommandLineChanges, occurs,
                //     the command line options will not be available and we should bail if one of the project references does not give us anything.
                let mutable canBail = false

                let referencedProjects = ResizeArray()

                if project.AreFSharpInMemoryCrossProjectReferencesEnabled then
                    for projectReference in project.ProjectReferences do
                        let referencedProject = project.Solution.GetProject(projectReference.ProjectId)

                        if referencedProject.Language = FSharpConstants.FSharpLanguageName then
                            match! tryComputeOptions referencedProject with
                            | ValueNone -> canBail <- true
                            | ValueSome(_, projectOptions) ->
                                referencedProjects.Add(
                                    FSharpReferencedProject.FSharpReference(referencedProject.OutputFilePath, projectOptions)
                                )
                        elif referencedProject.SupportsCompilation then
                            let! comp = referencedProject.GetCompilationAsync(ct)
                            let peRef = createPEReference referencedProject comp
                            referencedProjects.Add(peRef)

                if canBail then
                    return ValueNone
                else
                    match tryGetProjectSite project with
                    | ValueNone -> return ValueNone
                    | ValueSome projectSite ->

                        let otherOptions =
                            [|
                                // Clear any references from CompilationOptions.
                                // We get the references from Project.ProjectReferences/Project.MetadataReferences.
                                for x in projectSite.CompilationOptions do
                                    if not (x.Contains("-r:")) then
                                        x

                                for x in project.MetadataReferences.OfType<PortableExecutableReference>() do
                                    "-r:" + x.FilePath

                                for x in project.ProjectReferences do
                                    "-r:" + project.Solution.GetProject(x.ProjectId).OutputFilePath

                                // In the IDE we always ignore all #line directives for all purposes.  This means
                                // IDE features work correctly within generated source files, but diagnostics are
                                // reported in the IDE with respect to the generated source, and will not unify with
                                // diagnostics from the build.
                                "--ignorelinedirectives"
                            |]

                        let! ver = project.GetDependentVersionAsync(ct)

                        let projectOptions =
                            {
                                ProjectFileName = projectSite.ProjectFileName
                                ProjectId = Some(projectId.ToFSharpProjectIdString())
                                SourceFiles = projectSite.CompilationSourceFiles
                                OtherOptions = otherOptions
                                ReferencedProjects = referencedProjects.ToArray()
                                IsIncompleteTypeCheckEnvironment = projectSite.IsIncompleteTypeCheckEnvironment
                                UseScriptResolutionRules = CompilerEnvironment.MustBeSingleFileProject(Path.GetFileName(project.FilePath))
                                LoadTime = projectSite.LoadTime
                                UnresolvedReferences = None
                                OriginalLoadReferences = []
                                Stamp = Some(int64 (ver.GetHashCode()))
                            }

                        // This can happen if we didn't receive the callback from HandleCommandLineChanges yet.
                        if Array.isEmpty projectOptions.SourceFiles then
                            return ValueNone
                        else
                            // Clear any caches that need clearing and invalidate the project.
                            let currentSolution = project.Solution.Workspace.CurrentSolution

                            let projectsToClearCache =
                                cache |> Seq.filter (fun pair -> not (currentSolution.ContainsProject pair.Key))

                            if not (Seq.isEmpty projectsToClearCache) then
                                projectsToClearCache
                                |> Seq.iter (fun pair -> cache.TryRemove pair.Key |> ignore)

                                let options =
                                    projectsToClearCache
                                    |> Seq.map (fun pair ->
                                        let _, _, projectOptions = pair.Value
                                        projectOptions)

                                checker.ClearCache(options, userOpName = "tryComputeOptions")

                            lastSuccessfulCompilations.ToArray()
                            |> Array.iter (fun pair ->
                                if not (currentSolution.ContainsProject(pair.Key)) then
                                    lastSuccessfulCompilations.TryRemove(pair.Key) |> ignore)

                            checker.InvalidateConfiguration(projectOptions, userOpName = "tryComputeOptions")

                            let parsingOptions, _ = checker.GetParsingOptionsFromProjectOptions(projectOptions)

                            cache.[projectId] <- (project, parsingOptions, projectOptions)

                            return ValueSome(parsingOptions, projectOptions)

            | true, (oldProject, parsingOptions, projectOptions) ->
                if isProjectInvalidated oldProject project ct then
                    cache.TryRemove(projectId) |> ignore
                    return! tryComputeOptions project ct
                else
                    return ValueSome(parsingOptions, projectOptions)
        }

    let loop (agent: MailboxProcessor<FSharpProjectOptionsMessage>) =
        async {
            while true do
                match! agent.Receive() with
                | FSharpProjectOptionsMessage.TryGetOptionsByDocument(document, reply, ct, userOpName) ->
                    if ct.IsCancellationRequested then
                        reply.Reply ValueNone
                    else
                        try
                            // For now, disallow miscellaneous workspace since we are using the hacky F# miscellaneous files project.
                            if document.Project.Solution.Workspace.Kind = WorkspaceKind.MiscellaneousFiles then
                                reply.Reply ValueNone
                            elif document.Project.IsFSharpMiscellaneousOrMetadata then
                                let! options =
                                    tryComputeOptionsBySingleScriptOrFile document userOpName
                                    |> CancellableTask.start ct
                                    |> Async.AwaitTask

                                if ct.IsCancellationRequested then
                                    reply.Reply ValueNone
                                else
                                    reply.Reply options
                            else
                                // We only care about the latest project in the workspace's solution.
                                // We do this to prevent any possible cache thrashing in FCS.
                                let project =
                                    document.Project.Solution.Workspace.CurrentSolution.GetProject(document.Project.Id)

                                if not (isNull project) then
                                    let! options = tryComputeOptions project |> CancellableTask.start ct |> Async.AwaitTask

                                    if ct.IsCancellationRequested then
                                        reply.Reply ValueNone
                                    else
                                        reply.Reply options
                                else
                                    reply.Reply ValueNone
                        with _ ->
                            reply.Reply ValueNone

                | FSharpProjectOptionsMessage.TryGetOptionsByProject(project, reply, ct) ->
                    if ct.IsCancellationRequested then
                        reply.Reply ValueNone
                    else
                        try
                            if
                                project.Solution.Workspace.Kind = WorkspaceKind.MiscellaneousFiles
                                || project.IsFSharpMiscellaneousOrMetadata
                            then
                                reply.Reply ValueNone
                            else
                                // We only care about the latest project in the workspace's solution.
                                // We do this to prevent any possible cache thrashing in FCS.
                                let project = project.Solution.Workspace.CurrentSolution.GetProject(project.Id)

                                if not (isNull project) then
                                    let! options = tryComputeOptions project |> CancellableTask.start ct |> Async.AwaitTask

                                    if ct.IsCancellationRequested then
                                        reply.Reply ValueNone
                                    else
                                        reply.Reply options
                                else
                                    reply.Reply ValueNone
                        with _ ->
                            reply.Reply ValueNone

                | FSharpProjectOptionsMessage.ClearOptions(projectId) ->
                    match cache.TryRemove(projectId) with
                    | true, (_, _, projectOptions) ->
                        lastSuccessfulCompilations.TryRemove(projectId) |> ignore
                        checker.ClearCache([ projectOptions ])
                    | _ -> ()

                    legacyProjectSites.TryRemove(projectId) |> ignore
                | FSharpProjectOptionsMessage.ClearSingleFileOptionsCache(documentId) ->
                    match singleFileCache.TryRemove(documentId) with
                    | true, (_, _, _, projectOptions, subscription) ->
                        lastSuccessfulCompilations.TryRemove(documentId.ProjectId) |> ignore
                        checker.ClearCache([ projectOptions ])
                        subscription |> Option.iter (fun handler -> handler.Dispose())
                    | _ -> ()
        }

    let agent =
        MailboxProcessor.Start((fun agent -> loop agent), cancellationToken = cancellationTokenSource.Token)

    member _.TryGetOptionsByProjectAsync(project, ct) =
        agent.PostAndAsyncReply(fun reply -> FSharpProjectOptionsMessage.TryGetOptionsByProject(project, reply, ct))

    member _.TryGetOptionsByDocumentAsync(document, ct, userOpName) =
        agent.PostAndAsyncReply(fun reply -> FSharpProjectOptionsMessage.TryGetOptionsByDocument(document, reply, ct, userOpName))

    member _.ClearOptionsByProjectId(projectId) =
        agent.Post(FSharpProjectOptionsMessage.ClearOptions(projectId))

    member _.ClearSingleFileOptionsCache(documentId) =
        agent.Post(FSharpProjectOptionsMessage.ClearSingleFileOptionsCache(documentId))

    member _.SetCommandLineOptions(projectId, sourcePaths, options) =
        commandLineOptions.[projectId] <- (sourcePaths, options)

    member _.SetLegacyProjectSite(projectId, projectSite) =
        legacyProjectSites.[projectId] <- projectSite

    member _.TryGetCachedOptionsByProjectId(projectId) =
        match cache.TryGetValue(projectId) with
        | true, result -> Some(result)
        | _ -> None

    member _.ClearAllCaches() =
        commandLineOptions.Clear()
        legacyProjectSites.Clear()
        cache.Clear()
        singleFileCache.Clear()
        lastSuccessfulCompilations.Clear()

    member _.ScriptUpdated = scriptUpdatedEvent.Publish

    interface IDisposable with
        member _.Dispose() =
            cancellationTokenSource.Cancel()
            cancellationTokenSource.Dispose()
            (agent :> IDisposable).Dispose()

/// Manages mappings of Roslyn workspace Projects/Documents to FCS.
type internal FSharpProjectOptionsManager(checker: FSharpChecker, workspace: Workspace) =

    let reactor = new FSharpProjectOptionsReactor(checker)

    do
        // We need to listen to this event for lifecycle purposes.
        workspace.WorkspaceChanged.Add(fun args ->
            match args.Kind with
            | WorkspaceChangeKind.ProjectRemoved -> reactor.ClearOptionsByProjectId(args.ProjectId)
            | _ -> ())

        workspace.DocumentClosed.Add(fun args ->
            let doc = args.Document
            let proj = doc.Project

            if proj.IsFSharp && proj.IsFSharpMiscellaneousOrMetadata then
                reactor.ClearSingleFileOptionsCache(doc.Id))

    member _.ScriptUpdated = reactor.ScriptUpdated

    member _.SetLegacyProjectSite(projectId, projectSite) =
        reactor.SetLegacyProjectSite(projectId, projectSite)

    /// Clear a project from the project table
    member _.ClearInfoForProject(projectId: ProjectId) =
        reactor.ClearOptionsByProjectId(projectId)

    member _.ClearSingleFileOptionsCache(documentId) =
        reactor.ClearSingleFileOptionsCache(documentId)

    /// Get compilation defines and language version relevant for syntax processing.
    /// Quicker then TryGetOptionsForDocumentOrProject as it doesn't need to recompute the exact project
    /// options for a script.
    member _.GetCompilationDefinesAndLangVersionForEditingDocument(document: Document) =
        let parsingOptions =
            match reactor.TryGetCachedOptionsByProjectId(document.Project.Id) with
            | Some(_, parsingOptions, _) -> parsingOptions
            | _ ->
                { FSharpParsingOptions.Default with
                    ApplyLineDirectives = false
                    IsInteractive = CompilerEnvironment.IsScriptFile document.Name
                }

        CompilerEnvironment.GetConditionalDefinesForEditing parsingOptions, parsingOptions.LangVersionText, parsingOptions.StrictIndentation

    member _.TryGetOptionsByProject(project) =
        reactor.TryGetOptionsByProjectAsync(project)

    /// Get the exact options for a document or project
    member _.TryGetOptionsForDocumentOrProject(document: Document, cancellationToken, userOpName) =
        reactor.TryGetOptionsByDocumentAsync(document, cancellationToken, userOpName)

    /// Get the exact options for a document or project relevant for syntax processing.
    member this.TryGetOptionsForEditingDocumentOrProject(document: Document, cancellationToken, userOpName) =
        this.TryGetOptionsForDocumentOrProject(document, cancellationToken, userOpName)

    /// Get the options for a document or project relevant for syntax processing.
    /// Quicker it doesn't need to recompute the exact project options for a script.
    member this.TryGetQuickParsingOptionsForEditingDocumentOrProject(documentId: DocumentId, path: string) =
        match reactor.TryGetCachedOptionsByProjectId(documentId.ProjectId) with
        | Some(_, parsingOptions, _) -> parsingOptions
        | _ ->
            { FSharpParsingOptions.Default with
                IsInteractive = CompilerEnvironment.IsScriptFile path
            }

    member _.SetCommandLineOptions(projectId, sourcePaths, options: ImmutableArray<string>) =
        reactor.SetCommandLineOptions(projectId, sourcePaths, options.ToArray())

    member _.ClearAllCaches() = reactor.ClearAllCaches()

    member _.Checker = checker
