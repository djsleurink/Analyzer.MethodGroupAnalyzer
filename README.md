# Method group inside iterators

Analyzer which detects unnecesary instantion of delegates (Action/Func) and provides possible solutions to fix this issue.

The issue is fixed as of .NET 7.0 so the analyzer will not be used in projects targeting .NET 7.0.

2 possible fixes are provided. 
- Declaring a local variable outside the scope of an iterator.
- Convert to the method group to a static lambda (C#9.0+)
![](ActionAsLocalVariable.gif)