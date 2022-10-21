// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.Language

open Xunit
open FSharp.Test.Compiler

module SealedAndAbstractClassAttributeCheckingTests =

    [<Fact>]
    let ``When Sealed and AbstractClass attributes are used at same time(Static class) 1`` () =
        Fsx """
[<Sealed; AbstractClass>]
type T =
    abstract A : int
    abstract B : int with get, set
    abstract C : i:int -> int
    abstract D : i:int -> int
    default _.D i = i + 3
    member _.E = 3
    val F : int
    val mutable G : int
    member _.H (i, j) = i + j
    static member X = 0;
    member _.Item with get i = 3 and set i value = ()
    override _.ToString () = "🙃"
    new () = { F = 3; G = 3 }
    new (x, y) = { F = x; G = y }
        """
         |> withLangVersionPreview
         |> compile
         |> shouldFail
         |> withDiagnostics [
             (Error 3551, Line 4, Col 5, Line 17, Col 34, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. You can only static members.")
         ]

    [<Fact>]
    let ``When Sealed and AbstractClass attributes are used at same time(Static class) 2`` () =
        Fsx """
[<Sealed; AbstractClass>]
type T =
    abstract A : int
    abstract B : int with get, set
    abstract C : i:int -> int
    abstract D : i:int -> int
    default _.D i = i + 3
    member _.E = 3
    val F : int
    val mutable G : int
    member _.H (i, j) = i + j
    static member X = 0;
    member _.Item with get i = 3 and set i value = ()
    override _.ToString () = "🙃"
    new () = { F = 3; G = 3 }
    new (x, y) = { F = x; G = y }
        """
         |> withLangVersion70
         |> compile
         |> shouldSucceed