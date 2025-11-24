using Parlot.Fluent;
using System.Collections.Generic;
using Xunit;

#nullable enable

namespace Parlot.Tests;

public class HybridListTests
{
    [Fact]
    public void Empty_InitiallyEmpty()
    {
        var list = new HybridList<int>();
        Assert.Empty(list);
    }

    [Fact]
    public void Add_SingleItem()
    {
        var list = new HybridList<int>();
        list.Add(1);
        
        Assert.Single(list);
        Assert.Equal(1, list[0]);
    }

    [Fact]
    public void Add_FourItems_InlineStorage()
    {
        var list = new HybridList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        Assert.Equal(4, list.Count);
        Assert.Equal(1, list[0]);
        Assert.Equal(2, list[1]);
        Assert.Equal(3, list[2]);
        Assert.Equal(4, list[3]);
    }

    [Fact]
    public void Add_FiveItems_TransitionsToList()
    {
        var list = new HybridList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);
        list.Add(5);

        Assert.Equal(5, list.Count);
        Assert.Equal(1, list[0]);
        Assert.Equal(2, list[1]);
        Assert.Equal(3, list[2]);
        Assert.Equal(4, list[3]);
        Assert.Equal(5, list[4]);
    }

    [Fact]
    public void Add_ManyItems_AfterTransition()
    {
        var list = new HybridList<int>();
        for (int i = 1; i <= 10; i++)
        {
            list.Add(i);
        }

        Assert.Equal(10, list.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(i + 1, list[i]);
        }
    }

    [Fact]
    public void Indexer_OutOfRange_ThrowsException()
    {
        var list = new HybridList<int>();
        list.Add(1);

        Assert.Throws<System.ArgumentOutOfRangeException>(() => list[-1]);
        Assert.Throws<System.ArgumentOutOfRangeException>(() => list[1]);
    }

    [Fact]
    public void Indexer_OutOfRange_AfterTransition_ThrowsException()
    {
        var list = new HybridList<int>();
        for (int i = 1; i <= 5; i++)
        {
            list.Add(i);
        }

        Assert.Throws<System.ArgumentOutOfRangeException>(() => list[-1]);
        Assert.Throws<System.ArgumentOutOfRangeException>(() => list[5]);
    }

    [Fact]
    public void Enumerate_InlineStorage()
    {
        var list = new HybridList<int>();
        list.Add(10);
        list.Add(20);
        list.Add(30);

        var items = new List<int>();
        foreach (var item in list)
        {
            items.Add(item);
        }

        Assert.Equal(3, items.Count);
        Assert.Equal(10, items[0]);
        Assert.Equal(20, items[1]);
        Assert.Equal(30, items[2]);
    }

    [Fact]
    public void Enumerate_AfterTransition()
    {
        var list = new HybridList<int>();
        for (int i = 1; i <= 7; i++)
        {
            list.Add(i);
        }

        var items = new List<int>();
        foreach (var item in list)
        {
            items.Add(item);
        }

        Assert.Equal(7, items.Count);
        for (int i = 0; i < 7; i++)
        {
            Assert.Equal(i + 1, items[i]);
        }
    }

    [Fact]
    public void GetEnumerator_InlineStorage_Empty()
    {
        var list = new HybridList<int>();

        var items = new List<int>();
        foreach (var item in list)
        {
            items.Add(item);
        }

        Assert.Empty(items);
    }

    [Fact]
    public void Add_WithStrings()
    {
        var list = new HybridList<string>();
        list.Add("alpha");
        list.Add("beta");
        list.Add("gamma");

        Assert.Equal(3, list.Count);
        Assert.Equal("alpha", list[0]);
        Assert.Equal("beta", list[1]);
        Assert.Equal("gamma", list[2]);
    }

    [Fact]
    public void Add_WithNullValues()
    {
        var list = new HybridList<string?>();
        list.Add(null);
        list.Add("value");
        list.Add(null);

        Assert.Equal(3, list.Count);
        Assert.Null(list[0]);
        Assert.Equal("value", list[1]);
        Assert.Null(list[2]);
    }

    [Fact]
    public void Enumerate_WithNullValues()
    {
        var list = new HybridList<string?>();
        list.Add(null);
        list.Add("a");
        list.Add(null);
        list.Add("b");
        list.Add(null);

        var items = new List<string?>();
        foreach (var item in list)
        {
            items.Add(item);
        }

        Assert.Equal(5, items.Count);
        Assert.Null(items[0]);
        Assert.Equal("a", items[1]);
        Assert.Null(items[2]);
        Assert.Equal("b", items[3]);
        Assert.Null(items[4]);
    }

    [Fact]
    public void Add_LargeCount()
    {
        var list = new HybridList<int>();
        const int count = 1000;

        for (int i = 0; i < count; i++)
        {
            list.Add(i);
        }

        Assert.Equal(count, list.Count);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(i, list[i]);
        }
    }

    [Fact]
    public void Enumerate_LargeCount()
    {
        var list = new HybridList<int>();
        const int count = 1000;

        for (int i = 0; i < count; i++)
        {
            list.Add(i);
        }

        var index = 0;
        foreach (var item in list)
        {
            Assert.Equal(index, item);
            index++;
        }

        Assert.Equal(count, index);
    }

    [Fact]
    public void IReadOnlyListInterface()
    {
        IReadOnlyList<int> list = new HybridList<int>();
        ((HybridList<int>)(object)list).Add(1);
        ((HybridList<int>)(object)list).Add(2);
        ((HybridList<int>)(object)list).Add(3);

        Assert.Equal(3, list.Count);
        Assert.Equal(1, list[0]);
        Assert.Equal(2, list[1]);
        Assert.Equal(3, list[2]);

        var items = new List<int>();
        foreach (var item in list)
        {
            items.Add(item);
        }
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public void TransitionPoint_ExactlyAtFourItems()
    {
        var list = new HybridList<int>();
        list.Add(100);
        list.Add(200);
        list.Add(300);
        list.Add(400);
        
        // Should still use inline storage
        Assert.Equal(4, list.Count);
        Assert.Equal(100, list[0]);
        Assert.Equal(400, list[3]);
    }

    [Fact]
    public void TransitionPoint_JustAfterFourItems()
    {
        var list = new HybridList<int>();
        list.Add(100);
        list.Add(200);
        list.Add(300);
        list.Add(400);
        list.Add(500);
        
        // Should have transitioned to List<T>
        Assert.Equal(5, list.Count);
        Assert.Equal(100, list[0]);
        Assert.Equal(500, list[4]);
    }
}
