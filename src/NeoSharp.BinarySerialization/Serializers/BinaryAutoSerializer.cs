﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NeoSharp.BinarySerialization.Cache;
using NeoSharp.BinarySerialization.SerializationHooks;

namespace NeoSharp.BinarySerialization.Serializers
{
    internal class BinaryAutoSerializer : IBinaryCustomSerializable
    {
        /// <summary>
        /// Type
        /// </summary>
        public readonly Type Type;
        /// <summary>
        /// IsEmpty
        /// </summary>
        public readonly bool IsEmpty;

        /// <summary>
        /// Cache entries
        /// </summary>
        private readonly BinarySerializerCacheEntry[] _entries;
        /// <summary>
        /// Serializer
        /// </summary>
        private readonly IBinaryCustomSerializable _serializer;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="type">Type</param>
        /// <param name="serializer">Serializer</param>
        public BinaryAutoSerializer(Type type, IBinaryCustomSerializable serializer)
        {
            Type = type;
            _serializer = serializer;
            IsEmpty = false;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="type">Type</param>
        public BinaryAutoSerializer(Type type)
        {
            Type = type;

            // Check interfaces

            var serializerAttr = type.GetCustomAttribute<BinaryTypeSerializerAttribute>();

            if (serializerAttr != null)
            {
                // By serializer
                _serializer = serializerAttr.Create();
                IsEmpty = false;
            }
            else
            {
                _entries =

                // Properties

                type.GetProperties()
                .Select(u => new { prop = u, atr = u.GetCustomAttribute<BinaryPropertyAttribute>(true) })
                .Where(u => u.atr != null)
                .OrderBy(u => u.atr.Order)
                .Select(u => new BinarySerializerCacheEntry(u.atr, u.prop))
                .Concat
                (
                    // Fields

                    type.GetFields()
                    .Select(u => new { prop = u, atr = u.GetCustomAttribute<BinaryPropertyAttribute>(true) })
                    .Where(u => u.atr != null)
                    .OrderBy(u => u.atr.Order)
                    .Select(u => new BinarySerializerCacheEntry(u.atr, u.prop))
                )
                .ToArray();

                IsEmpty = _entries.Length <= 0;
            }
        }

        /// <summary>
        /// Serialize
        /// </summary>
        /// <param name="serializer">Serializer</param>
        /// <param name="bw">Stream</param>
        /// <param name="obj">Object</param>
        /// <param name="settings">Settings</param>
        public int Serialize(IBinarySerializer serializer, BinaryWriter bw, object obj, BinarySerializerSettings settings = null)
        {
            if (_serializer != null)
            {
                return _serializer.Serialize(serializer, bw, obj, settings);
            }

            var ret = 0;
            var haveFilter = settings != null && settings.Filter != null;

            foreach (BinarySerializerCacheEntry e in _entries)
            {
                if (haveFilter && !settings.Filter.Invoke(e.Context.Order)) continue;

                ret += e.Serializer.Serialize(serializer, bw, e.GetValue(obj));
            }

            return ret;
        }
        /// <summary>
        /// Deserialize
        /// </summary>
        /// <param name="deserializer">Deserializer</param>
        /// <param name="reader">Reader</param>
        /// <param name="settings">Settings</param>
        /// <returns>Deserialized object</returns>
        public T Deserialize<T>(IBinaryDeserializer deserializer, BinaryReader reader, BinarySerializerSettings settings = null)
        {
            return (T)Deserialize(deserializer, reader, Type, settings);
        }
        /// <summary>
        /// Deserialize object
        /// </summary>
        /// <param name="deserializer">Deserializer</param>
        /// <param name="reader">Reader</param>
        /// <param name="settings">Settings</param>
        /// <returns>Deserialized object</returns>
        public object Deserialize(IBinaryDeserializer deserializer, BinaryReader reader, BinarySerializerSettings settings = null)
        {
            return Deserialize(deserializer, reader, Type, settings);
        }

        /// <summary>
        /// Deserialize object
        /// </summary>
        /// <param name="deserializer">Deserializer</param>
        /// <param name="reader">Reader</param>
        /// <param name="type">Type</param>
        /// <param name="settings">Settings</param>
        /// <returns>Deserialized object</returns>
        public object Deserialize(IBinaryDeserializer deserializer, BinaryReader reader, Type type, BinarySerializerSettings settings = null)
        {
            if (_serializer != null)
            {
                var ret = _serializer.Deserialize(deserializer, reader, type, settings);

                if (ret is IBinaryVerifiable v && !v.Verify())
                {
                    throw new FormatException();
                }

                return ret;
            }
            else
            {
                object ret;
                var haveFilter = settings != null && settings.Filter != null;

                ret = Activator.CreateInstance(type);

                foreach (BinarySerializerCacheEntry e in _entries)
                {
                    if (haveFilter && !settings.Filter.Invoke(e.Context.Order)) continue;

                    if (e.ReadOnly)
                    {
                        // Consume it
                        e.Serializer.Deserialize(deserializer, reader, e.Type, settings);
                        continue;
                    }

                    e.SetValue(ret, e.Serializer.Deserialize(deserializer, reader, e.Type, settings));
                }

                if (ret is IBinaryVerifiable v && !v.Verify())
                {
                    throw new FormatException();
                }

                return ret;
            }
        }
    }
}