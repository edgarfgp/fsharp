// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.Language

open Xunit
open FSharp.Test.Compiler

module SealedAndAbstractClassAttributeCheckingTests =
    [<Fact>]
    let ``Sealed and AbstractClass on a type in lang version70`` () =
        Fsx """
[<Sealed; AbstractClass>]
type T = class end
        """
         |> withLangVersion70
         |> compile
         |> shouldSucceed
         
    [<Fact>]
    let ``Sealed and AbstractClass on a type in lang preview`` () =
        Fsx """
[<Sealed; AbstractClass>]
type T = class end
        """
         |> withLangVersionPreview
         |> compile
         |> shouldSucceed
         
    [<Fact>]
    let ``Sealed and AbstractClass on a type with constructor in lang preview`` () =
        Fsx """
[<Sealed; AbstractClass>]
type T() = class end
        """
         |> withLangVersionPreview
         |> compile
         |> shouldSucceed
         
    [<Fact>]
    let ``Sealed and AbstractClass on a type with constructor in lang version70`` () =
        Fsx """
[<Sealed; AbstractClass>]
type T() = class end
        """
         |> withLangVersion70
         |> compile
         |> shouldSucceed
         
    [<Fact>]
    let ``Sealed and AbstractClass on a type with constructor with arguments in lang preview`` () =
        Fsx """
[<Sealed; AbstractClass>]
type T(x: int) = class end
        """
         |> withLangVersionPreview
         |> compile
         |> shouldFail
         |> withDiagnostics [
                (Error 3551, Line 3, Col 7, Line 3, Col 15, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No constructor with arguments is allowed")
         ]
         
    [<Fact>]
    let ``Sealed and AbstractClass on a type with constructor with arguments in lang version70`` () =
        Fsx """
[<Sealed; AbstractClass>]
type T(x: int) = class end
        """
         |> withLangVersion70
         |> compile
         |> shouldSucceed

    [<Fact>]
    let ``When Sealed and AbstractClass on a type with additional constructors in lang preview`` () =
        Fsx """
[<Sealed; AbstractClass>]
type T =
    new () = {}
        """
         |> withLangVersionPreview
         |> compile
         |> shouldFail
         |> withDiagnostics [
            (Error 3551, Line 4, Col 5, Line 4, Col 16, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No additional constructor is allowed")
         ]
         
    [<Fact>]
    let ``When Sealed and AbstractClass on a type with additional constructors in lang version70`` () =
        Fsx """
[<Sealed; AbstractClass>]
type T =
    new () = {}
        """
         |> withLangVersion70
         |> compile
         |> shouldSucceed

    [<Fact>]
    let ``When Sealed and AbstractClass on a type with a primary(parameters) and additional constructor in lang preview`` () =
        Fsx """
[<Sealed; AbstractClass>]
type T(x: int) =
    new () = {}
        """
         |> withLangVersionPreview
         |> compile
         |> shouldFail
         |> withDiagnostics [
            (Error 3551, Line 3, Col 7, Line 3, Col 15, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No constructor with arguments is allowed")
            (Error 3551, Line 4, Col 5, Line 4, Col 16, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No additional constructor is allowed")
         ]

    [<Fact>]
    let ``When Sealed and AbstractClass on a type with a primary(parameters) and additional constructor in lang version70`` () =
        Fsx """
[<Sealed; AbstractClass>]
type T(x: int) =
    new () = {}
        """
         |> withLangVersion70
         |> compile
         |> shouldFail
         |> withDiagnostics [
             (Error 762, Line 4, Col 14, Line 4, Col 16, "Constructors for the type 'T' must directly or indirectly call its implicit object constructor. Use a call to the implicit object constructor instead of a record expression.")
         ]
         
    [<Fact>]
    let ``When Sealed and AbstractClass on a type with explicit fields and constructor in lang version70`` () =
        Fsx """
[<Sealed; AbstractClass>]
type B =
    val F : int
    val mutable G : int
    new () = { F = 3; G = 3 }
        """
         |> withLangVersion70
         |> compile
         |> shouldSucceed
         
    [<Fact>]
    let ``When Sealed and AbstractClass on a type with explicit fields and constructor in lang preview`` () =
        Fsx """
[<Sealed; AbstractClass>]
type B =
    val F : int
    val mutable G : int
    new () = { F = 3; G = 3 }
        """
         |> withLangVersionPreview
         |> compile
         |> shouldFail
         |> withDiagnostics [
             (Error 3551, Line 4, Col 5, Line 4, Col 16, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No explicit fields is allowed")
             (Error 3551, Line 5, Col 5, Line 5, Col 24, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No explicit fields is allowed")
             (Error 3551, Line 6, Col 5, Line 6, Col 30, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No additional constructor is allowed")
         ]
         
    [<Fact>]
    let ``When Sealed and AbstractClass on a type with instance members in lang version70`` () =
        Fsx """
[<Sealed; AbstractClass>]
type B =
    member _.H (i, j) = i + j
    member _.E = 3
    member _.Item with get i = 3 and set i value = ()
        """
         |> withLangVersion70
         |> compile
         |> shouldSucceed
         
    [<Fact>]
    let ``When Sealed and AbstractClass on a type with instance members in lang preview`` () =
        Fsx """
[<Sealed; AbstractClass>]
type B =
    member _.H (i, j) = i + j
    member _.E = 3
    member _.Item with get i = 3 and set i value = ()
    override _.ToString () = "BRB"
        """
         |> withLangVersionPreview
         |> compile
         |> shouldFail
         |> withDiagnostics [
             (Error 3551, Line 4, Col 5, Line 4, Col 30, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No instance members is allowed")
             (Error 3551, Line 5, Col 5, Line 5, Col 19, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No instance members is allowed")
             (Error 3551, Line 6, Col 5, Line 6, Col 54, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No instance members is allowed")
             (Error 3551, Line 7, Col 5, Line 7, Col 35, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No instance members is allowed")
         ]
         
    [<Fact>]
    let ``When Sealed and AbstractClass on a type with abstract members in lang version70`` () =
        Fsx """
[<Sealed; AbstractClass>]
type B =
    abstract A : int
    abstract B : int
    abstract C : int
    abstract D : i:int -> int
    default _.D i = i + 3
        """
         |> withLangVersion70
         |> compile
         |> shouldSucceed
         
    [<Fact>]
    let ``When Sealed and AbstractClass on a type with abstract members in lang preview`` () =
        Fsx """
[<Sealed; AbstractClass>]
type B =
    abstract A : int
    abstract B : int
    abstract C : int
    abstract D : i:int -> int
    default _.D i = i + 3
        """
         |> withLangVersionPreview
         |> compile
         |> shouldFail
         |> withDiagnostics [
             (Error 3551, Line 4, Col 5, Line 4, Col 21, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No abstract members is allowed")
             (Error 3551, Line 5, Col 5, Line 5, Col 21, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No abstract members is allowed")
             (Error 3551, Line 6, Col 5, Line 6, Col 21, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No abstract members is allowed")
             (Error 3551, Line 7, Col 5, Line 7, Col 30, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No abstract members is allowed")
             (Error 3551, Line 8, Col 5, Line 8, Col 26, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No instance members is allowed")
         ]
         
    [<Fact>]
    let ``When Sealed and AbstractClass on a type with primary, additional constructors, explicit field, instance and abstract members lang version70`` () =
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
    member _.Item with get i = 3 and set i value = ()
    override _.ToString () = "BBB"
    new () = { F = 3; G = 3 }
    new (x, y) = { F = x; G = y }
        """
         |> withLangVersion70
         |> compile
         |> shouldSucceed
         
    [<Fact>]
    let ``When Sealed and AbstractClass on a type with primary, additional constructors, explicit field, instance and abstract members in lang preview`` () =
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
    member _.Item with get i = 3 and set i value = ()
    override _.ToString () = "BBB"
    new () = { F = 3; G = 3 }
    new (x, y) = { F = x; G = y }
        """
         |> withLangVersionPreview
         |> compile
         |> shouldFail
         |> withDiagnostics [
             (Error 3551, Line 4, Col 5, Line 4, Col 21, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No abstract members is allowed")
             (Error 3551, Line 5, Col 5, Line 5, Col 35, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No abstract members is allowed")
             (Error 3551, Line 6, Col 5, Line 6, Col 30, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No abstract members is allowed")
             (Error 3551, Line 7, Col 5, Line 7, Col 30, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No abstract members is allowed")
             (Error 3551, Line 8, Col 5, Line 8, Col 26, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No instance members is allowed")
             (Error 3551, Line 9, Col 5, Line 9, Col 19, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No instance members is allowed")
             (Error 3551, Line 10, Col 5, Line 10, Col 16, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No explicit fields is allowed")
             (Error 3551, Line 11, Col 5, Line 11, Col 24, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No explicit fields is allowed")
             (Error 3551, Line 12, Col 5, Line 12, Col 30, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No instance members is allowed")
             (Error 3551, Line 13, Col 5, Line 13, Col 54, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No instance members is allowed")
             (Error 3551, Line 14, Col 5, Line 14, Col 35, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No instance members is allowed")
             (Error 3551, Line 15, Col 5, Line 15, Col 30, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No additional constructor is allowed")
             (Error 3551, Line 16, Col 5, Line 16, Col 34, "if a type uses both [<Sealed>] and [<AbstractClass>] it means it is static. No additional constructor is allowed")
         ]
         
    [<Fact>]
    let ``When Sealed and AbstractClass on a type with static members lang version70`` () =
        Fsx """
[<Sealed; AbstractClass>]
type B =    
    static member Z = "Z"
    static member X  c = c
        """
         |> withLangVersion70
         |> compile
         |> shouldSucceed
         
    [<Fact>]
    let ``When Sealed and AbstractClass on a type with static members in lang preview`` () =
        Fsx """
type B =    
    static member Z = "Z"
    static member X  c = c
        """
         |> withLangVersionPreview
         |> compile
         |> shouldSucceed
         
    [<Fact>]
    let ``When Sealed and AbstractClass on a type with static let bindings and members lang version70`` () =
        Fsx """
[<Sealed; AbstractClass>]
type B() =
    static let x c = 1 + c
    static member Z = "Z"
    static member X  c = x c
        """
         |> withLangVersion70
         |> compile
         |> shouldSucceed
         
    [<Fact>]
    let ``When Sealed and AbstractClass on a type with static let bindings and members in lang preview`` () =
        Fsx """
[<Sealed; AbstractClass>]
type B() =
    static let x c = 1 + c
    static member Z = "Z"
    static member X c = x c
        """
         |> withLangVersionPreview
         |> compile
         |> shouldSucceed