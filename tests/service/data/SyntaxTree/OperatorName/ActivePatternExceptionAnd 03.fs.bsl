ImplFile
  (ParsedImplFileInput
     ("/root/OperatorName/ActivePatternExceptionAnd 03.fs", false,
      QualifiedNameOfFile ActivePatternExceptionAnd 03, [], [],
      [SynModuleOrNamespace
         ([ActivePatternExceptionAnd 03], false, AnonModule,
          [Exception
             (SynExceptionDefn
                (SynExceptionDefnRepr
                   ([],
                    SynUnionCase
                      ([], SynIdent (MyExn, None), Fields [], PreXmlDocEmpty,
                       None, (1,10--1,15), { BarRange = None }), None,
                    PreXmlDoc ((1,0), FSharp.Compiler.Xml.XmlDocCollector), None,
                    (1,0--1,15)), None, [], (1,0--1,15)), (1,0--1,15));
           Let
             (false,
              [SynBinding
                 (None, Normal, false, false, [],
                  PreXmlDoc ((3,0), FSharp.Compiler.Xml.XmlDocCollector),
                  SynValData
                    (None, SynValInfo ([], SynArgInfo ([], false, None)), None),
                  Paren
                    (Ands
                       ([Typed
                           (Wild (3,5--3,6),
                            LongIdent (SynLongIdent ([exn], [], [None])),
                            (3,5--3,12));
                         Paren
                           (LongIdent
                              (SynLongIdent ([MyExn], [], [None]), None, None,
                               Pats [], None, (3,16--3,21)), (3,15--3,22))],
                        (3,5--3,22)), (3,4--3,23)), None,
                  App
                    (NonAtomic, false, Ident exn, Const (Unit, (3,30--3,32)),
                     (3,26--3,32)), (3,4--3,23), Yes (3,0--3,32),
                  { LeadingKeyword = Let (3,0--3,3)
                    InlineKeyword = None
                    EqualsRange = Some (3,24--3,25) })], (3,0--3,32))],
          PreXmlDocEmpty, [], None, (1,0--3,32), { LeadingKeyword = None })],
      (true, true), { ConditionalDirectives = []
                      WarnDirectives = []
                      CodeComments = [] }, set []))

(1,0)-(2,0) parse warning The declarations in this file will be placed in an implicit module 'ActivePatternExceptionAnd 03' based on the file name 'ActivePatternExceptionAnd 03.fs'. However this is not a valid F# identifier, so the contents will not be accessible from other files. Consider renaming the file or adding a 'module' or 'namespace' declaration at the top of the file.
