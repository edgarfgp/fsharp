// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Conformance.PatternMatching

open Xunit
open FSharp.Test
open FSharp.Test.Compiler

module NamedPatPairs =

    [<Theory; Directory(__SOURCE_DIRECTORY__, Includes=[|"NamedPatPairs01.fs"|])>]
    let ``Version9 NamedPatPairs - NamedPatPairs01_fs`` compilation =
        compilation
        |> ignoreWarnings
        |> withLangVersion90
        |> typecheck
        |> shouldFail
        |> withDiagnostics [
            (Error 3350, Line 7, Col 18, Line 7, Col 19, "Feature 'Allow comma as union case name field separator.' is not available in F# 9.0. Please use language version 'PREVIEW' or greater.");
            (Error 3350, Line 8, Col 25, Line 8, Col 26, "Feature 'Allow comma as union case name field separator.' is not available in F# 9.0. Please use language version 'PREVIEW' or greater.");
            (Error 3350, Line 8, Col 18, Line 8, Col 19, "Feature 'Allow comma as union case name field separator.' is not available in F# 9.0. Please use language version 'PREVIEW' or greater.")
        ]

    [<Theory; Directory(__SOURCE_DIRECTORY__, Includes=[|"NamedPatPairs01.fs"|])>]
    let ``Preview: NamedPatPairs - NamedPatPairs01_fs`` compilation =
        compilation
        |> ignoreWarnings
        |> withLangVersionPreview
        |> typecheck
        |> shouldSucceed
        
    [<Theory; Directory(__SOURCE_DIRECTORY__, Includes=[|"NamedPatPairs02.fs"|])>]
    let ``Version9 NamedPatPairs - NamedPatPairs02_fs`` compilation =
        compilation
        |> ignoreWarnings
        |> withLangVersion90
        |> typecheck
        |> shouldSucceed

    [<Theory; Directory(__SOURCE_DIRECTORY__, Includes=[|"NamedPatPairs02.fs"|])>]
    let ``Preview: NamedPatPairs - NamedPatPairs02_fs`` compilation =
        compilation
        |> ignoreWarnings
        |> withLangVersionPreview
        |> typecheck
        |> shouldSucceed
        
    [<Theory; Directory(__SOURCE_DIRECTORY__, Includes=[|"E_NamedPatPairs01.fs"|])>]
    let ``Version9 NamedPatPairs - E_NamedPatPairs01_fs`` compilation =
        compilation
        |> ignoreWarnings
        |> withLangVersion90
        |> typecheck
        |> shouldFail
        |> withDiagnostics [
            (Error 3879, Line 7, Col 25, Line 7, Col 26, "Inconsistent separators in pattern. Use either all commas or all semicolons, but not both.");
            (Error 3350, Line 7, Col 18, Line 7, Col 19, "Feature 'Allow comma as union case name field separator.' is not available in F# 9.0. Please use language version 'PREVIEW' or greater.")
        ]

    [<Theory; Directory(__SOURCE_DIRECTORY__, Includes=[|"E_NamedPatPairs01.fs"|])>]
    let ``Preview: NamedPatPairs - E_NamedPatPairs01_fs`` compilation =
        compilation
        |> ignoreWarnings
        |> withLangVersionPreview
        |> typecheck
        |> shouldFail
        |> withDiagnostics [
            (Error 3879, Line 7, Col 25, Line 7, Col 26, "Inconsistent separators in pattern. Use either all commas or all semicolons, but not both.")
        ]