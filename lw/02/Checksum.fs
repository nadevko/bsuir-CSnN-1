module CSnN1.Lw02.Checksum

let calculateChecksum (buffer : byte[]) : uint16 =
    let mutable sum = 0

    for i in 0..2 .. buffer.Length - 1 do
        let highByte = if i < buffer.Length then int buffer.[i] else 0
        let lowByte = if i + 1 < buffer.Length then int buffer.[i + 1] else 0
        let word = highByte <<< 8 ||| lowByte
        sum <- sum + word

    while sum >>> 16 > 0 do
        sum <- (sum &&& 0xFFFF) + (sum >>> 16)

    let result = ~~~(uint16 sum)

    result
