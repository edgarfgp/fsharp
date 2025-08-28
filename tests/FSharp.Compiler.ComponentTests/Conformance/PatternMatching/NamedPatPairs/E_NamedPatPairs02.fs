type RecordName = string * int

type MyUnion = 
    | CaseA of x: RecordName * y: string

let tupledNamed value =
    match value with
    | CaseA(x = a, b) -> sprintf "CaseB: a=%A b=%A" a b
