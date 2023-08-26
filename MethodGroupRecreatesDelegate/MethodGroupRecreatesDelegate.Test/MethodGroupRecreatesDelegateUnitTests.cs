using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;
using VerifyCS = MethodGroupRecreatesDelegate.Test.CSharpCodeFixVerifier<
    MethodGroupRecreatesDelegate.MethodGroupRecreatesDelegateAnalyzer,
    MethodGroupRecreatesDelegate.MethodGroupRecreatesDelegateCodeFixProvider>;
using Microsoft.CodeAnalysis.Testing;

namespace MethodGroupRecreatesDelegate.Test
{
    //proof for the validity of the use case:
    //private void memoryProof()
    //{
    //    //in ILSPY:
    //    for (int i = 0; i < 100000; i++)
    //    {
    //        doSomething(new Action(something));
    //    }
    //    //in C#:
    //    for (int i = 0; i < 100_000; i++)
    //    {
    //        doSomething(something);
    //    }

    //    //better (less memory alloc):
    //    Action action = something;
    //    for (int i = 0; i < 100_000; i++)
    //    {
    //        doSomething(action);
    //    }

    //    void doSomething(Action whateverINeedToDo) { }

    //    void something() { }
    //}
    //  //also better: static lambda (C#9.0)
    //  for (int i = 0; i < 100_000; i++)
    //    {
    //        doSomething(static () => {} );
    //    }

    [TestClass]
    public class MethodGroupRecreatesDelegateUnitTest
    {

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public async Task Analyzer_ActionAsMethodGroupNotInIteratorTest()
        {
            var test = _classBoilerPlate1 + @"
                        private void testMe(){
                            doSomething(something);
                        }
                    " + _classBoilerPlate2;
            await VerifyCS.VerifyAnalyzerAsync(test, DiagnosticResult.EmptyDiagnosticResults);

        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public async Task Analyzer_ActionAsMethodGroupInForLoopTest()
        {
            var test = _classBoilerPlate1 + @"
                        private void testMe(){
                            for(int i = 0; i < 10; i++)
                            {
                                doSomething(something);
                            }
                        }
                    " + _classBoilerPlate2;
            var fix = _classBoilerPlate1 + @"
                        private void testMe(){
                            var methodGroupVariable = new System.Action(something);
                            for(int i = 0; i < 10; i++)
                            {
                                doSomething(methodGroupVariable);
                            }
                        }
                    " + _classBoilerPlate2; ;
            var expectedResult = new Microsoft.CodeAnalysis.Testing.DiagnosticResult("MethodGroupRecreatesDelegate", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning).WithSpan(16, 45, 16, 54).WithArguments("System.Action", $"{Environment.NewLine}");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedResult);
            await VerifyCS.VerifyCodeFixAsync(test, expectedResult, fix);

        }
        [TestMethod]
        public async Task Analyzer_ActionAsMethodGroupInForEachLoopTest()
        {
            
            var test = _classBoilerPlate1 + @"
                        private void testMe(){
                            foreach(var item in Enumerable.Range(0, 1).ToList())
                            {
                                doSomething(something);
                            }
                        }
                    " + _classBoilerPlate2;
            var fix = _classBoilerPlate1 + @"
                        private void testMe(){
                            var methodGroupVariable = new System.Action(something);
                            foreach(var item in Enumerable.Range(0, 1).ToList())
                            {
                                doSomething(methodGroupVariable);
                            }
                        }
                    " + _classBoilerPlate2; ;
            var expectedResult = new Microsoft.CodeAnalysis.Testing.DiagnosticResult("MethodGroupRecreatesDelegate", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning).WithSpan(16, 45, 16, 54).WithArguments("System.Action", $"{Environment.NewLine}");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedResult);
            await VerifyCS.VerifyCodeFixAsync(test, expectedResult, fix);

        }
        [TestMethod]
        public async Task Analyzer_ActionAsMethodGroupInWhileLoopTest()
        {
            var test = _classBoilerPlate1 + @"
                        private void testMe(){
                            while(true)
                            {
                                doSomething(something);
                            }
                        }
                    " + _classBoilerPlate2;

            var fix = _classBoilerPlate1 + @"
                        private void testMe(){
                            var methodGroupVariable = new System.Action(something);
                            while(true)
                            {
                                doSomething(methodGroupVariable);
                            }
                        }
                    " + _classBoilerPlate2; ;
            var expectedResult = new Microsoft.CodeAnalysis.Testing.DiagnosticResult("MethodGroupRecreatesDelegate", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning).WithSpan(16, 45, 16, 54).WithArguments("System.Action", $"{Environment.NewLine}");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedResult);
            await VerifyCS.VerifyCodeFixAsync(test, expectedResult, fix);

        }
        [TestMethod]
        public async Task Analyzer_ActionAsMethodGroupInDoWhileLoopTest()
        {
            
            var test = _classBoilerPlate1 + @"
                        private void testMe(){
                            do
                            {
                                doSomething(something);
                            }
                            while(true);
                        }
                    " + _classBoilerPlate2;

            var fix = _classBoilerPlate1 + @"
                        private void testMe(){
                            var methodGroupVariable = new System.Action(something);
                            do
                            {
                                doSomething(methodGroupVariable);
                            }
                            while(true);
                        }
                    " + _classBoilerPlate2; ;
            var expectedResult = new Microsoft.CodeAnalysis.Testing.DiagnosticResult("MethodGroupRecreatesDelegate", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning).WithSpan(16, 45, 16, 54).WithArguments("System.Action", $"{Environment.NewLine}");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedResult);
            await VerifyCS.VerifyCodeFixAsync(test, expectedResult, fix);

        }
       
        [TestMethod]
        public async Task Analyzer_ActionAsLocalDeclaredActionTest()
        {
            var test = _classBoilerPlate1 + @"
                        private void testMe(){
                            Action a = something;
                            doSomething(a);
                        }
                    " + _classBoilerPlate2;

            await VerifyCS.VerifyAnalyzerAsync(test, DiagnosticResult.EmptyDiagnosticResults);
        }

        [TestMethod]
        public async Task Analyzer_GenericFuncAsMethodGroupInDoWhileLoopTest()
        {
            var test = _classBoilerPlate1 + @"
                        private void testMe(){
                            int i = 0;
                            do
                            {
                                ProcessNumbers<double>(Add, 5, 7);
                                i--;
                            }
                            while (i > 0);
                        }
                    " + _classBoilerPlate2;

            var fix = _classBoilerPlate1 + @"
                        private void testMe(){
                            int i = 0;
                            var methodGroupVariable = new System.Func<double, double, double>(Add);
                            do
                            {
                                ProcessNumbers<double>(methodGroupVariable, 5, 7);
                                i--;
                            }
                            while (i > 0);
                        }
                    " + _classBoilerPlate2; ;

            var expectedResult = new Microsoft.CodeAnalysis.Testing.DiagnosticResult("MethodGroupRecreatesDelegate", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning).WithSpan(17, 56, 17, 59).WithArguments("System.Func<double, double, double>", $"{Environment.NewLine}");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedResult);
            await VerifyCS.VerifyCodeFixAsync(test, expectedResult, fix);
        }

        private const string _classBoilerPlate1 = @"using System;
                using System.Collections.Generic;
                using System.Linq;
                using System.Text;
                using System.Threading.Tasks;
                using System.Diagnostics;

                namespace ConsoleApplication1
                {
                    class testclass
                    {   
                       ";

        private const string _classBoilerPlate2 = @" 
                    
                        private void doSomething(Action whateverINeedToDo) { }

                        private void something() { }

                        static T Add<T>(T a, T b)
                        {
                            return (dynamic)a + (dynamic)b;
                        }

                        static void ProcessNumbers<T>(Func<T, T, T> operation, T x, T y)
                        {
                            T result = operation(x, y);
                        }
                    }
                }";
    }

    internal class Proof
    {
        //void ProofMethod()
        //{
        //    doSomething(something);//doSomething(new System.Action(something));
        //    ProcessNumbers(Add, 1,2);//ProcessNumbers(new System.Func<int, int, int>(Add), 1, 2);
        //}

        //private IEnumerable xy
        //{
        //    get
        //    {
        //        
        //        yield return doSomethingYield(something); //gets compiled to MoveNext method: <>2__current = <>4__this.doSomethingYield(new System.Action(<>4__this.something));
        //    }
        //}


        //private object doSomethingYield(Action a)
        //{
        //    return default;
        //}

        //private  void doSomething(Action a)
        //{

        //}

        //private void something()
        //{

        //}


        //static T Add<T>(T a, T b)
        //{
        //    return (dynamic)a + (dynamic)b;
        //}

        //static void ProcessNumbers<T>(Func<T, T, T> operation, T x, T y)
        //{
        //    T result = operation(x, y);
        //    Console.WriteLine("Result: " + result);
        //}
    }
}
