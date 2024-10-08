// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

open System.IO
open System.Text

type integer = { alias : string; name : string; suffix : string; signed : bool; width : int }

let integers = 
    [
        { alias = "byte";  name = "Byte";    suffix = "uy"; signed = false; width = 8  };
        { alias = "sbyte"; name = "SByte";   suffix = "y";  signed = true;  width = 8  };        
 
        { alias = "int8";   name="Int8";     suffix="y";    signed = true;  width = 8  };
        { alias = "uint8";  name="UInt8";    suffix="uy";   signed = false; width = 8  };
 
        { alias = "int16";  name = "Int16";  suffix = "s";  signed = true;  width = 16 };        
        { alias = "uint16"; name = "UInt16"; suffix = "us"; signed = false; width = 16 };        
        
        { alias = "int32";  name = "Int32";  suffix = "l";  signed = true;  width = 32 };
        { alias = "uint32"; name = "UInt32"; suffix = "ul"; signed = false; width = 32 };

        { alias = "int64";  name = "Int64";  suffix = "L";  signed = true;  width = 64 };
        { alias = "uint64"; name = "UInt64"; suffix = "uL"; signed = false; width = 64 };                                
    ]
    
let maxValueAsHexLiteral typ =
    let sb = new StringBuilder("0x") 
    let halfByteLength = typ.width / 4
    sb.Append('F', halfByteLength).ToString()

do 
    use file = new StreamWriter("IntConversionsGenerated.fs")
    
    // indentation 'framework'
    let mutable depth = 0
    let indent () =
        for i = 1 to depth do
            file.Write("  ") 
    let shift () = depth <- depth + 1
    let unshift () = depth <- depth - 1
    let prn (format : Format<'T,TextWriter,unit,unit>) : 'T = 
        indent (); fprintfn<'T> file format 
    
    let preamble = @"
// This file is automatically generated by IntConversionsTestGenerator.fsx    
namespace FSharp.Core.UnitTests        
open System                           
open XUnit
open FSharp.Core.UnitTests.LibraryTestFx

module UInt8 =
    let MinValue = Byte.MinValue
    let MaxValue = Byte.MaxValue

module Int8 =
    let MinValue = SByte.MinValue
    let MaxValue = SByte.MaxValue

type IntConversionsGenerated() =
"
                
    file.WriteLine(preamble)
    shift ()
    
    let signedInts = integers |> List.filter (fun t -> t.signed)
    let unsignedInts = integers |> List.filter (fun t -> not t.signed)    
    
    do // Unchecked
        // -1 converted to unsigned types. Should become MaxValues
        let signedToUnsignedGenerator target =
            signedInts |> List.iter (fun source->
                prn "[<Fact>]"
                prn "member this.``%s.m1.To.%s`` () =" source.alias target.alias
                shift ()
                prn "let i : %s = -1%s" source.alias source.suffix
                prn "Assert.AreEqual (%s.MaxValue, %s i)" target.name target.alias 
                unshift ()
                prn "")                    
        unsignedInts |> List.iter signedToUnsignedGenerator

        // -1 converted to signed types. Should stay -1.
        let signedToSignedGenerator target =
            signedInts |> List.iter (fun source->
                prn "[<Fact>]"
                prn "member this.``%s.m1.To.%s`` () =" source.alias target.alias
                shift ()
                prn "let minus1 : %s = -1%s" target.alias target.suffix
                prn "let i : %s = -1%s" source.alias source.suffix
                prn "Assert.AreEqual (minus1, %s i)" target.alias 
                unshift ()
                prn "")        
        signedInts |> List.iter signedToSignedGenerator
    
        // Unsigned MaxValues converted to wider types. Should remain the same.
        let unsignedToWiderGenerator target = 
            unsignedInts |> List.filter (fun source -> source.width < target.width)
            |> List.iter (fun source ->
                    prn "[<Fact>]"
                    prn "member this.``%s.MaxValue.To.%s`` () =" source.alias target.alias
                    shift ()                
                    prn "let sourceMaxValue : %s = %s%s" target.alias (maxValueAsHexLiteral source) target.suffix
                    prn "Assert.AreEqual (sourceMaxValue, %s %s.MaxValue)" target.alias source.name
                    unshift ()
                    prn "")
        integers |> List.iter unsignedToWiderGenerator
    
        // Unsigned MaxValues converted to narrower or same width signed. Should become -1.
        let unsignedToNarrowerSignedGenerator target =
            unsignedInts |> List.filter (fun source -> source.width >= target.width)
            |> List.iter (fun source ->
                    prn "[<Fact>]"
                    prn "member this.``%s.MaxValue.To.%s`` () =" source.alias target.alias
                    shift ()
                    prn "Assert.AreEqual (-1%s, %s %s.MaxValue)" target.suffix target.alias source.name
                    unshift ()
                    prn "")            
        signedInts |> List.iter unsignedToNarrowerSignedGenerator
    
        // Unsigned MaxValues converted to narrower or same width unsigned. Should become target's MaxValues
        let unsignedToNarrowerUnsignedGenerator target =
            unsignedInts |> List.filter (fun source -> source.width >= target.width)
            |> List.iter (fun source ->
                prn "[<Fact>]"
                prn "member this.``%s.MaxValue.To.%s`` () ="  source.alias target.alias
                shift ()
                prn "Assert.AreEqual (%s.MaxValue, %s %s.MaxValue)" target.name target.alias source.name
                unshift ()
                prn "")  
        unsignedInts |> List.iter unsignedToNarrowerUnsignedGenerator 
    
        // -1 to signed nativeint stays -1
        signedInts |> List.iter (fun source ->
            prn "[<Fact>]"
            prn "member this.``%s.m1.To.nativeint`` () =" source.alias
            shift ()
            prn "Assert.AreEqual (-1n, nativeint -1%s)" source.suffix
            unshift ()
            prn "") 
            
        // unsigned MaxValues to signed nativeint stay same for narrower types, become -1 for wider types
        unsignedInts |> List.iter (fun source ->
            prn "[<Fact>]"
            prn "member this.``%s.MaxValue.To.nativeint`` () =" source.alias
            shift ()
            prn "if sizeof<nativeint> > sizeof<%s> then" source.alias
            prn "   let sourceMaxValue : nativeint = %sn" (maxValueAsHexLiteral source)
            prn "   Assert.AreEqual (sourceMaxValue, nativeint %s.MaxValue)" source.name
            prn "else"
            prn "   Assert.AreEqual (-1n, nativeint %s.MaxValue)" source.name
            unshift ()
            prn "") 
        
        prn "member private this.UnativeintMaxValue ="
        shift ()
        prn "let mutable unativeintMaxValue : unativeint = 0un"
        prn "for i = 1 to sizeof<nativeint> do"
        prn "   unativeintMaxValue <- (unativeintMaxValue <<< 8) ||| 0xFFun"
        prn "unativeintMaxValue"
        unshift ()
        prn ""
    
        // -1 to unsigned nativeint should become MaxValue
        signedInts |> List.iter (fun source ->
            prn "[<Fact>]"
            prn "member this.``%s.m1.To.unativeint`` () =" source.alias
            shift ()        
            prn "Assert.AreEqual (this.UnativeintMaxValue, unativeint -1%s)" source.suffix
            unshift ()
            prn "") 
     
        // unsigned MaxValues to unsigned nativeint stay same for narrower types, become MaxValue for wider types
        unsignedInts |> List.iter (fun source ->
            prn "[<Fact>]"
            prn "member this.``%s.m1.To.unativeint`` () =" source.alias
            shift ()        
            prn "if sizeof<unativeint> > sizeof<%s> then" source.alias
            prn "   let sourceMaxValue : unativeint = %sun" (maxValueAsHexLiteral source)
            prn "   Assert.AreEqual (sourceMaxValue, unativeint %s.MaxValue)" source.name
            prn "else"
            prn "   Assert.AreEqual (this.UnativeintMaxValue, unativeint %s.MaxValue)" source.name
            unshift ()
            prn "") 

    do // Checked
        let signedInts = signedInts |> List.filter (fun t -> t.alias <> "int8")
        let unsignedInts = unsignedInts |> List.filter (fun t -> t.alias <> "uint8")
        // -1 converted to unsigned types. Should throw
        let checkedSignedToUnsignedGenerator target =
            signedInts |> List.iter (fun source->
                prn "[<Fact>]"
                prn "member this.``Checked.%s.m1.To.%s`` () =" source.alias target.alias
                shift ()
                prn "let i : %s = -1%s" source.alias source.suffix
                prn "CheckThrowsExn<OverflowException>(fun () -> Checked.%s i |> ignore)" target.alias 
                unshift ()
                prn "")                    
        unsignedInts |> List.iter checkedSignedToUnsignedGenerator

        // -1 converted to signed types. Should stay -1.
        let signedToSignedGenerator target =
            signedInts |> List.iter (fun source->
                prn "[<Fact>]"
                prn "member this.``Checked.%s.m1.To.%s`` () =" source.alias target.alias
                shift ()
                prn "let minus1 : %s = -1%s" target.alias target.suffix
                prn "let i : %s = -1%s" source.alias source.suffix
                prn "Assert.AreEqual (minus1, Checked.%s i)" target.alias 
                unshift ()
                prn "")        
        signedInts |> List.iter signedToSignedGenerator
    
        // Unsigned MaxValues converted to wider types. Should remain the same.
        let checkedUnsignedToWiderGenerator target = 
            unsignedInts |> List.filter (fun source -> source.width < target.width)
            |> List.iter (fun source ->
                    prn "[<Fact>]"
                    prn "member this.``Checked.%s.MaxValue.To.%s`` () =" source.alias target.alias
                    shift ()                
                    prn "let sourceMaxValue : %s = %s%s" target.alias (maxValueAsHexLiteral source) target.suffix
                    prn "Assert.AreEqual (sourceMaxValue, Checked.%s %s.MaxValue)" target.alias source.name
                    unshift ()
                    prn "")
        integers |> List.iter checkedUnsignedToWiderGenerator
    
        // Unsigned MaxValues converted to narrower or same width signed. Should throw.
        let checkedUnsignedToNarrowerSignedGenerator target =
            unsignedInts |> List.filter (fun source -> source.width >= target.width && target.alias <> "int8")
            |> List.iter (fun source ->
                    prn "[<Fact>]"
                    prn "member this.``Checked.%s.MaxValue.To.%s`` () =" source.alias target.alias
                    shift ()
                    prn "CheckThrowsExn<OverflowException> (fun () -> Checked.%s %s.MaxValue |> ignore)" target.alias source.name
                    unshift ()
                    prn "")            
        signedInts |> List.iter checkedUnsignedToNarrowerSignedGenerator
    
        // Unsigned MaxValues converted to narrower or same width unsigned. Should throw
        let checkedUnsignedToNarrowerUnsignedGenerator target =
            unsignedInts |> List.filter (fun source -> source.width > target.width)
            |> List.iter (fun source ->
                prn "[<Fact>]"
                prn "member this.``Checked.%s.MaxValue.To.%s`` () ="  source.alias target.alias
                shift ()
                prn "CheckThrowsExn<OverflowException> (fun () -> Checked.%s %s.MaxValue |> ignore)" target.alias source.name
                unshift ()
                prn "")  
        unsignedInts |> List.iter checkedUnsignedToNarrowerUnsignedGenerator 
    
        // -1 to signed nativeint stays -1
        signedInts |> List.iter (fun source ->
            prn "[<Fact>]"
            prn "member this.``Checked.%s.m1.To.nativeint`` () =" source.alias
            shift ()
            prn "Assert.AreEqual (-1n, Checked.nativeint -1%s)" source.suffix
            unshift ()
            prn "") 
            
        // unsigned MaxValues to signed nativeint stay same for narrower types, throw for wider types
        unsignedInts |> List.iter (fun source ->
            prn "[<Fact>]"
            prn "member this.``Checked.%s.MaxValue.To.nativeint`` () =" source.alias
            shift ()
            prn "if sizeof<nativeint> > sizeof<%s> then" source.alias
            prn "   let sourceMaxValue : nativeint = %sn" (maxValueAsHexLiteral source)
            prn "   Assert.AreEqual (sourceMaxValue, Checked.nativeint %s.MaxValue)" source.name
            prn "else"
            prn "   CheckThrowsExn<OverflowException> (fun () -> Checked.nativeint %s.MaxValue |> ignore)" source.name
            unshift ()
            prn "") 
        
    
        // -1 to unsigned nativeint should throw
        signedInts |> List.iter (fun source ->
            prn "[<Fact>]"
            prn "member this.``Checked.%s.m1.To.unativeint`` () =" source.alias
            shift ()        
            prn "CheckThrowsExn<OverflowException> (fun () -> Checked.unativeint -1%s |> ignore)" source.suffix
            unshift ()
            prn "") 
     
        // unsigned MaxValues to unsigned nativeint stay same for narrower types, throw for wider types
        unsignedInts |> List.iter (fun source ->
            prn "[<Fact>]"
            prn "member this.``Checked.%s.MaxValue.To.unativeint`` () =" source.alias
            shift ()        
            prn "if sizeof<unativeint> >= sizeof<%s> then" source.alias
            prn "   let sourceMaxValue : unativeint = %sun" (maxValueAsHexLiteral source)
            prn "   Assert.AreEqual (sourceMaxValue, Checked.unativeint %s.MaxValue)" source.name
            prn "else"
            prn "   CheckThrowsExn<OverflowException> (fun () -> Checked.unativeint %s.MaxValue |> ignore)" source.name
            unshift ()
            prn "") 

