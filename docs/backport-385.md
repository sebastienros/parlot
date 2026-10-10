# Custom whitespace cache

Changing `ParseContext.WhiteSpaceParser` invalidates the cached whitespace skip, including when entering or leaving `WithComments` and `WithWhiteSpaceParser`. Ordered choices respect each custom whitespace parser instead of skipping whitespace using the default parser ahead of the choice.
