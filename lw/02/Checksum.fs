module CSnN1.Lw02.Checksum

/// Вычисляет стандартную контрольную сумму интернет-протоколов (IP, UDP, ICMP и др.)
/// Сумма 16-битных слов с переносом и дополнение до единицы.
/// buffer - массив байтов для подсчета контрольной суммы
/// zeroIfChecksum - если true, значение 0 будет заменено на 0xFFFF (как в UDP)
let calculateChecksum (buffer: byte[]) : uint16 =
    let mutable sum = 0

    // Суммирование 16-битных слов
    for i in 0..2 .. buffer.Length - 1 do
        let highByte = if i < buffer.Length then int buffer.[i] else 0
        let lowByte = if i + 1 < buffer.Length then int buffer.[i + 1] else 0
        let word = (highByte <<< 8) ||| lowByte
        sum <- sum + word

    // Добавление переносов
    while sum >>> 16 > 0 do
        sum <- (sum &&& 0xFFFF) + (sum >>> 16)

    // Дополнение до единицы
    let result = ~~~(uint16 sum)

    result
