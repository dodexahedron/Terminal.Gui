﻿using Xunit.Abstractions;
using static Terminal.Gui.Dim;

namespace Terminal.Gui.PosDimTests;

public class DimFunctionTests (ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;


    [Fact]
    public void DimFunction_Equal ()
    {
        Func<int> f1 = () => 0;
        Func<int> f2 = () => 0;

        Dim dim1 = Dim.Function (f1);
        Dim dim2 = Dim.Function (f2);
        Assert.Equal (dim1, dim2);

        f2 = () => 1;
        dim2 = Dim.Function (f2);
        Assert.NotEqual (dim1, dim2);
    }

    [Fact]
    public void DimFunction_SetsValue ()
    {
        var text = "Test";
        Dim dim = Dim.Function (() => text.Length);
        Assert.Equal ("DimFunc(4)", dim.ToString ());

        text = "New Test";
        Assert.Equal ("DimFunc(8)", dim.ToString ());

        text = "";
        Assert.Equal ("DimFunc(0)", dim.ToString ());
    }


    [Fact]
    public void DimFunction_Calculate_ReturnsCorrectValue ()
    {
        var dim = new DimFunc (() => 10);
        var result = dim.Calculate (0, 100, null, Dim.Dimension.None);
        Assert.Equal (10, result);
    }
}
