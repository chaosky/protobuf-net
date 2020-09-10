# protobuf 性能优化及 GC 优化总结

## protobuf-net 分析

[protobuf-net](https://github.com/protobuf-net/protobuf-net) 当前的 GC 问题有：

- 序列化
  - 反射。函数`ProtoBuf.Serializers.PropertyDecorator.Write`中的`property.GetValue(value, null)`
  - 装箱拆箱。函数`ProtoBuf.Serializers.PropertyDecorator.Write`中的`Tail.Write(value, dest)`
  - foreach。`ProtoBuf.Serializers.ListDecorator.Write`中的`foreach (object subItem in (IEnumerable)value)`
- 反序列化GC
  - 反射。函数`ProtoBuf.Serializers.PropertyDecorator.Read`中的`property.SetValue`
  - 装箱拆箱。函数`ProtoBuf.Serializers.PropertyDecorator.Read`中的`Tail.Read(oldVal, source)`
  - 列表创建。函数`ProtoBuf.Serializers.ListDecorator.Read`中的`value = Activator.CreateInstance(concreteType)`
  - 列表扩容。函数`ProtoBuf.Serializers.ListDecorator.Read`中的`list.Add`
  - byte[]创建。函数`ProtoBuf.ProtoReader.AppendBytes`中的字节数组创建
  - Pb对象创建。函数`ProtoBuf.Serializers.TypeSerializer.Read`中的`CreateInstance(source)`

## protobuf-net-gc-optimization 针对 protobuf-net 进行的优化

[protobuf-net-gc-optimization](https://github.com/smilehao/protobuf-net-gc-optimization)

- 去反射：对指定的协议进行Hook。
  - 反射产生的地方在protobuf-net的装饰类中，具体是PropertyDecorator，没有去写工具自动生成Wrap文件，而是对指定的协议进行Hook。`CustomDecorator`, `ICustomProtoSerializer`及其实现
- foreach
  - foreach 对列表来说改写遍历方式，没有对它进行优化，因为 Unity 5.5 以后版本这个问题就不存在了
- 无GC装箱
  - 消除装箱操作。重构代码，而 protobuf-net 内部大量使用了object 进行参数传递，这使得用泛型编程来消除 GC 变得不太现实。实现了一个无 GC 版本的装箱拆箱类`ValueObject`
- 使用对象池
  - 对于 protobuf-net反序列化的时候会创建pb对象这一点，最合理的方式是使用对象池，Hook 住protobuf-net 创建对象的地方，从对象池中取对象，而不是新建对象，用完以后再执行回收。`IProtoPool`及实现
- 使用字节缓存池
  - 对于 new byte[] 操作的 GC 优化也是一样的，只不过这里使用的缓存池是针对字节数组而非 pb 对象，实现了一套通用的字节流与字节 buffer 缓存池`StreamBufferPool`，每次需要字节buffer时从中取，用完以后放回。

### protobuf-net-gc-optimization 的其他优化

- BetterDelegate：泛型委托包装类，针对深层函数调用树中使用泛型委托作为函数参数进行传递时代码编写困难的问题。
- BetterLinkedList：无GC链表
- BetterStringBuilder：无GC版StrigBuilder
- StreamBufferPool：字节流与字节buffer缓存池
- ValueObject：无GC装箱拆箱
- ObjPool：通用对象池

关键节点：

- LinkedList当自定义结构做链表节点，必须实现IEquatable<T>、IComparable<T>接口，否则Roemove、Cotains、Find、FindLast每次都有GC产生
- 所有委托必须缓存，产生GC的测试一律是因为每次调用都生成了一个新的委托
- List<T>对于自定义结构做列表项，必须实现IEquatable<T>、IComparable<T>接口，否则Roemove、Cotains、IndexOf、sort每次都有GC产生；对于Sort，需要传递一个委托。这两点的实践上面都已经说明。

## 针对 protobuf-net-gc-optimization 的优化

[protobuf-net-gc-optimization](https://github.com/smilehao/protobuf-net-gc-optimization) 使用的`protobuf-net`是 2015 年之前的版本，当前项目使用的是 protobuf-net 2.4.5, 把 protobuf-net-gc-optimization 相关的优化合并到了 protobuf-net 2.x 版本上。

### foreach

`ProtoBuf.Serializers.ListDecorator.Write`中的`foreach (object subItem in (IEnumerable)value)`

因为 C# 不支持泛型协变，上述 foreach 循环还会产生GC，需要针对性的优化。

优化前：

```csharp
foreach (object subItem in (IEnumerable)value)
{
    if (checkForNull && subItem == null) { throw new NullReferenceException(); }
    Tail.Write(ValueObject.TryGet(subItem), dest);
}
```

优化后：

```csharp
if (value is IList list)
{
    for (int i = 0; i < list.Count; i++)
    {
        var subItem = list[i];
        if (checkForNull && subItem == null) { throw new NullReferenceException(); }
        Tail.Write(ValueObject.TryGet(subItem), dest);
    }
}
else
{
    foreach (object subItem in (IEnumerable)value)
    {
        if (checkForNull && subItem == null) { throw new NullReferenceException(); }
        Tail.Write(ValueObject.TryGet(subItem), dest);
    }
}
```

### 其他优化

`protobuf-net`的`BufferPool`在[2018.6.8](https://github.com/protobuf-net/protobuf-net/commit/9718b9221ee0c2aa13509d0a258a0728d3fc3210#diff-3df8aa4e7ab0d7118f25612197fbe78d)修改为`弱引用(WeakReference)`实现内部缓存，之前的[老版本](https://github.com/protobuf-net/protobuf-net/commit/15fa224b3ceab2cdf99012d999307b3435936665#diff-3df8aa4e7ab0d7118f25612197fbe78d)。弱引用会导致 Unity Profile 时，每次调用缓存失效，创建新的对象（56B）。这里使用老的版本。

## 需要再次确认的代码

- `ProtoBuf.BufferPool`内部有锁，用于 ProtoWriter，ProtoReader 读写, 能否优化掉
- `ProtoBuf.ProtoReader.AppendBytes`: `protobuf-net-gc-optimization`的注释（// TODO：这里还有漏洞，但是我们目前的项目不会走到这）需要再次确认

## 测试结果

`Assets/TestScenes/TestProtoBuf/TestProtoBuf.unity` 及 `TestProtoBuf.Test5` 测试结果, 开启 deep profile：

|             | 序列化 GC/time | 反序列化GC GC/time |
| ----------- | -------------- | ------------------ |
| 优化前₁     | 0.8k/0.21ms    | 1.3k/0.23ms        |
| 优化后₂     | 80B+56B/0.23ms | 0B+56B/0.20ms      |
| 再次优化后₃ | 0B             | 0B                 |

1. https://github.com/protobuf-net/protobuf-net 代码
2. https://github.com/smilehao/protobuf-net-gc-optimization 修改合并到 protobuf-net 2.4.5 后。两次List枚举器的获取，每次40B
3. 2.4.5-gc-optimization 代码

## google protobuf 分析

[protocolbuffers/protobuf 3.13.0](https://github.com/protocolbuffers/protobuf/tree/v3.13.0)需要 .NET Standard 2.1，Unity 只支持 .NET Standard 2.0，需要添加额外的DLL[1](https://github.com/protocolbuffers/protobuf/issues/7668), [2](https://github.com/protocolbuffers/protobuf/issues/7252)。

## 参考资料

- [九：Unity 帧同步补遗（性能优化）](https://zhuanlan.zhihu.com/p/39478710)
- [Unity3D游戏GC优化总结---protobuf-net无GC版本优化实践](https://www.cnblogs.com/SChivas/p/7898166.html)
- [Google protobuf 重用缓存方法](https://github.com/protocolbuffers/protobuf/issues/644)
- [unity 官方 GC 优化教程 - Fixing Performance Problems - 2019.3](https://learn.unity.com/tutorial/fixing-performance-problems-2019-3?uv=2019.3#)
