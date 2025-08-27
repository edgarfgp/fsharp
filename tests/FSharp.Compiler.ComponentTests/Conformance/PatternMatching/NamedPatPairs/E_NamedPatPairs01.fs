type MyUnion = 
    | CaseA of a: int * b: string
    | CaseB of x: float * y: bool * z: char

let testMixed value =
    match value with
    | CaseB(x = p, y = q; z = r) -> sprintf "CaseB: x=%f, y=%b, z=%c" p q r
