ImplFile
  (ParsedImplFileInput
     ("/root/SynType/Fun 05.fs", false, QualifiedNameOfFile Module, [], [],
      [SynModuleOrNamespace
         ([Module], false, NamedModule,
          [Expr
             (Paren
                (Typed
                   (Const (Unit, (3,1--3,3)),
                    Fun
                      (LongIdent (SynLongIdent ([a], [], [None])),
                       Fun
                         (FromParseError (3,10--3,10),
                          LongIdent (SynLongIdent ([c], [], [None])),
                          (3,10--3,14), { ArrowRange = (3,10--3,12) }),
                       (3,5--3,14), { ArrowRange = (3,7--3,9) }), (3,1--3,14)),
                 (3,0--3,1), Some (3,14--3,15), (3,0--3,15)), (3,0--3,15))],
          PreXmlDoc ((1,0), FSharp.Compiler.Xml.XmlDocCollector), [], None,
          (1,0--3,15), { LeadingKeyword = Module (1,0--1,6) })], (true, true),
      { ConditionalDirectives = []
        WarnDirectives = []
        CodeComments = [] }, set []))

(3,10)-(3,12) parse error Expecting type
