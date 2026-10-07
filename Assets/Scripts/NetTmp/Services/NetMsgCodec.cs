using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using MessagePack;

namespace FPSGame.Net
{
    /// <summary>
    /// 【网络消息序列化】02_Net 的 DTO 专用编解码器 —— **AOT（IL2CPP）安全**。
    ///
    /// <para>▍为什么不用 <c>MessagePackSerializer.Serialize</c>：
    /// 它默认走 <c>StandardResolver → DynamicObjectResolver</c>，后者用 <c>System.Reflection.Emit</c>
    /// 现造 formatter；**IL2CPP 不支持动态代码生成** ⇒ 打包版一发包就抛
    /// <c>PlatformNotSupportedException: Operation is not supported on this platform</c>
    /// （<c>AssemblyBuilder.DefineDynamicAssembly</c>）。编辑器走 Mono，所以完全看不出来。</para>
    ///
    /// <para>▍这里怎么绕：只用 MessagePack 的 <see cref="MessagePackWriter"/> / <see cref="MessagePackReader"/>
    /// 写读**标准 MessagePack 字节流**，成员枚举与字段读写自己做（反射 + 类型分派）。
    /// 全程不碰 Reflection.Emit、不需要任何预生成 formatter，也不必给每个新消息手写 Serialize/Deserialize
    /// —— 新增消息只要照旧加 <c>[MessagePackObject]</c> + <c>[Key(n)]</c> 就能通。</para>
    ///
    /// <para>▍线格式：<c>[MessagePackObject]</c> 类型写成 **array 格式**（长度 = 成员数，按 <c>[Key]</c> 升序），
    /// 与 MessagePack 原生 DynamicObjectResolver 对 int key 的默认选择一致（最省字节，
    /// 且让"新字段追加在末尾 ⇒ 旧端拿默认值"的兼容语义保持不变）。</para>
    ///
    /// <para>▍⚠ 只支持 <see cref="WriteValue"/> / <see cref="ReadValue"/> 里列出的字段类型。
    /// 用到没列出的类型会在**编辑器里就抛 NotSupportedException**（宁可当场报错，也不要静默写坏字节）。
    /// ⚠ 反射用法对 IL2CPP 托管裁剪是"不可见"的 ⇒ 见 <c>Assets/link.xml</c> 里对 02_Net 的保留声明。</para>
    /// </summary>
    public static class NetMsgCodec
    {
        /// <summary>[Key] 成员描述：key（决定 array 里的顺序）+ 类型 + 读写委托。</summary>
        private sealed class Member
        {
            public int Key;
            public Type Type;
            public Func<object, object> Get;
            public Action<object, object> Set;
        }

        private static readonly ConcurrentDictionary<Type, Member[]> _members = new();

        #region 对外入口

        /// <summary>把消息对象序列化成 MessagePack 字节（array 格式）。</summary>
        /// <param name="msg">消息实例（不可为 null）</param>
        /// <param name="type">消息类型（发送端的 <c>typeof(T)</c>，与接收端注册的类型一致）</param>
        public static byte[] Serialize(object msg, Type type)
        {
            if (msg == null) throw new ArgumentNullException(nameof(msg));

            var buffer = new ArrayBufferWriter<byte>(64);
            var writer = new MessagePackWriter(buffer);
            WriteValue(ref writer, msg, type);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }

        /// <summary>把 MessagePack 字节还原成消息对象。</summary>
        /// <param name="type">消息类型（注册处理器时保存的 <see cref="Type"/>）</param>
        /// <param name="data"><see cref="Serialize"/> 产出的字节</param>
        public static object Deserialize(Type type, byte[] data)
        {
            var reader = new MessagePackReader(new ReadOnlyMemory<byte>(data));
            return ReadValue(ref reader, type);
        }

        #endregion

        #region 写

        private static void WriteValue(ref MessagePackWriter writer, object value, Type type)
        {
            // 引用类型为 null（string / 数组 / 嵌套 DTO）⇒ 写 Nil，读端还原成 null
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            if (type == typeof(string)) { writer.Write((string)value); return; }
            if (type == typeof(int)) { writer.Write((int)value); return; }
            if (type == typeof(uint)) { writer.Write((uint)value); return; }
            if (type == typeof(long)) { writer.Write((long)value); return; }
            if (type == typeof(float)) { writer.Write((float)value); return; }
            if (type == typeof(bool)) { writer.Write((bool)value); return; }
            if (type == typeof(short)) { writer.Write((short)value); return; }
            if (type == typeof(byte)) { writer.Write((byte)value); return; }
            if (type == typeof(double)) { writer.Write((double)value); return; }
            if (type.IsEnum) { writer.Write(Convert.ToInt32(value)); return; }

            if (type.IsArray)
            {
                Type element = type.GetElementType();
                var array = (Array)value;
                writer.WriteArrayHeader(array.Length);
                for (int i = 0; i < array.Length; ++i)
                {
                    WriteValue(ref writer, array.GetValue(i), element);
                }
                return;
            }

            Member[] members = TryGetMembers(type);
            if (members != null)
            {
                writer.WriteArrayHeader(members.Length);
                for (int i = 0; i < members.Length; ++i)
                {
                    WriteValue(ref writer, members[i].Get(value), members[i].Type);
                }
                return;
            }

            throw new NotSupportedException(Unsupported(type));
        }

        #endregion

        #region 读

        private static object ReadValue(ref MessagePackReader reader, Type type)
        {
            if (type == typeof(string)) return reader.ReadString();
            if (type == typeof(int)) return reader.ReadInt32();
            if (type == typeof(uint)) return reader.ReadUInt32();
            if (type == typeof(long)) return reader.ReadInt64();
            if (type == typeof(float)) return reader.ReadSingle();
            if (type == typeof(bool)) return reader.ReadBoolean();
            if (type == typeof(short)) return reader.ReadInt16();
            if (type == typeof(byte)) return reader.ReadByte();
            if (type == typeof(double)) return reader.ReadDouble();
            if (type.IsEnum) return Enum.ToObject(type, reader.ReadInt32());

            if (type.IsArray || type.IsClass)
            {
                if (reader.TryReadNil()) return null;
            }

            if (type.IsArray)
            {
                Type element = type.GetElementType();
                int length = reader.ReadArrayHeader();
                var array = Array.CreateInstance(element, length);
                for (int i = 0; i < length; ++i)
                {
                    array.SetValue(ReadValue(ref reader, element), i);
                }
                return array;
            }

            Member[] members = TryGetMembers(type);
            if (members != null)
            {
                object obj = Activator.CreateInstance(type);
                int count = reader.ReadArrayHeader();
                int assign = count < members.Length ? count : members.Length;
                for (int i = 0; i < assign; ++i)
                {
                    members[i].Set(obj, ReadValue(ref reader, members[i].Type));
                }
                // 对端字段比本端多（更新的版本）⇒ 多出来的值读掉丢掉，别让 reader 错位
                for (int i = members.Length; i < count; ++i)
                {
                    reader.Skip();
                }
                // 对端字段比本端少（更旧的版本）⇒ 末尾字段保持构造函数/字段初始化的默认值，正是需要的语义
                return obj;
            }

            throw new NotSupportedException(Unsupported(type));
        }

        #endregion

        #region 成员表

        /// <summary>
        /// 反射枚举成员表并缓存。返回 null = 这个类型不是网络 DTO（没有 [MessagePackObject] / 没有 [Key] 成员）。
        /// </summary>
        private static Member[] TryGetMembers(Type type)
        {
            if (!type.IsClass) return null;
            if (type.GetCustomAttribute<MessagePackObjectAttribute>(true) == null) return null;
            return _members.GetOrAdd(type, BuildMembers);
        }

        private static Member[] BuildMembers(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var list = new List<Member>();

            foreach (FieldInfo field in type.GetFields(flags))
            {
                KeyAttribute key = field.GetCustomAttribute<KeyAttribute>(true);
                if (key == null || !key.IntKey.HasValue) continue;

                FieldInfo captured = field;
                list.Add(new Member
                {
                    Key = key.IntKey.Value,
                    Type = captured.FieldType,
                    Get = captured.GetValue,
                    Set = captured.SetValue
                });
            }

            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                KeyAttribute key = property.GetCustomAttribute<KeyAttribute>(true);
                if (key == null || !key.IntKey.HasValue) continue;
                if (!property.CanRead || !property.CanWrite) continue;

                PropertyInfo captured = property;
                list.Add(new Member
                {
                    Key = key.IntKey.Value,
                    Type = captured.PropertyType,
                    Get = captured.GetValue,
                    Set = captured.SetValue
                });
            }

            if (list.Count == 0)
            {
                throw new NotSupportedException(
                    $"[NetMsgCodec] {type.FullName} 标了 [MessagePackObject] 却没有任何 [Key(0..n)] 成员。");
            }

            // array 格式靠"顺序"对齐，所以必须按 key 升序固定下来（两端一致才谈得上兼容）
            list.Sort((a, b) => a.Key.CompareTo(b.Key));
            return list.ToArray();
        }

        private static string Unsupported(Type type)
        {
            return $"[NetMsgCodec] 不支持的类型 {type.FullName}：" +
                   "DTO 请标 [MessagePackObject] + [Key(n)]；新字段类型请到 NetMsgCodec.WriteValue/ReadValue 里补分支。";
        }

        #endregion
    }
}
