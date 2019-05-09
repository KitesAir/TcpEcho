# TcpEcho
Basic TCP server that uses System.IO.Pipelines to parse length-prefixed messages.

This is just like [David Fowler's TcpEcho](https://github.com/davidfowl/TcpEcho), except it uses length prefixing instead of `\n` delimiters.
