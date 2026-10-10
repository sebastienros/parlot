# Empty left-associative matches

Both `LeftAssociative` overloads stop when an operator and its right operand match without consuming input. The zero-progress pair is not passed to the result factory. This prevents an infinite loop when optional or empty parsers are used; interpreted and expression-tree compiled parsers behave identically. Pairs consuming input are still applied even when one member is empty.
