# Signed decimal failures

`Scanner.ReadDecimal` restores its starting cursor position when a sign and decimal separator are not followed by a number. For example, failing to read `-.x` leaves the cursor at offset 0. This also applies to direct scanner callers.
