namespace ME.BECS.Editor.JSON {

    public abstract class ObjectReferenceSerializer<T, TValue> : SerializerBase<TValue> where T : UnityEngine.Object where TValue : unmanaged {

        public override object FromString(System.Type fieldType, string value) {
            var str = value;
            var protocol = $"{this.ProtocolPrefix}://";
            string customData = null;
            if (str.Contains('#') == true) {
                var splitted = str.Split('#', System.StringSplitOptions.RemoveEmptyEntries);
                str = splitted[0];
                customData = splitted[1];
            }

            if (str.StartsWith(protocol) == true) {
                var configObj = ObjectReferenceRegistry.GetAssetByPathPart<T>(str.Substring(protocol.Length));
                return this.Deserialize(ObjectReferenceRegistry.GetId(configObj), configObj, customData);
            } else {
                return null;
            }
        }

        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var customData = string.Empty;
            var entityConfig = ObjectReferenceRegistry.GetObjectBySourceId<T>(this.GetId((TValue)obj, ref customData));
            if (entityConfig == null) {
                builder.Append('"');
                builder.Append(this.ProtocolPrefix);
                builder.Append("://");
                builder.Append("null");
                if (string.IsNullOrEmpty(customData) == false) {
                    builder.Append('#');
                    builder.Append(customData);
                }
                builder.Append('"');
            } else {
                builder.Append('"');
                builder.Append(this.ProtocolPrefix);
                builder.Append("://");
                builder.Append(UnityEditor.AssetDatabase.GetAssetPath(entityConfig).Substring("Assets/".Length));
                if (string.IsNullOrEmpty(customData) == false) {
                    builder.Append('#');
                    builder.Append(customData);
                }
                builder.Append('"');
            }
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

        public abstract string ProtocolPrefix { get; }
        public abstract uint GetId(TValue obj, ref string customData);
        public abstract TValue Deserialize(uint objectId, T obj, string customData);

    }

    public abstract class PrimitiveSerializer<T> : SerializerBase<T> where T : System.IConvertible {
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (T)obj;
            builder.Append(val.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = obj;
        }
    }

    public class EnumSerializer : SerializerBase<System.Enum> {
        public override bool IsValid(System.Type type) => type.IsEnum;
        public override object FromString(System.Type fieldType, string value) => System.Enum.Parse(fieldType, value);
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (long)obj;
            builder.Append(val.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }
    }

    public class FloatSerializer : PrimitiveSerializer<float> {
        public override object FromString(System.Type fieldType, string value) => float.Parse(value);
    }

    public class DoubleSerializer : PrimitiveSerializer<double> {
        public override object FromString(System.Type fieldType, string value) => double.Parse(value);
    }

    public class DecimalSerializer : PrimitiveSerializer<decimal> {
        public override object FromString(System.Type fieldType, string value) => decimal.Parse(value);
    }

    public class IntSerializer : PrimitiveSerializer<int> {
        public override object FromString(System.Type fieldType, string value) => int.Parse(value);
    }

    public class UIntSerializer : PrimitiveSerializer<uint> {
        public override object FromString(System.Type fieldType, string value) => uint.Parse(value);
    }

    public class ShortSerializer : PrimitiveSerializer<short> {
        public override object FromString(System.Type fieldType, string value) => short.Parse(value);
    }

    public class UShortSerializer : PrimitiveSerializer<ushort> {
        public override object FromString(System.Type fieldType, string value) => ushort.Parse(value);
    }

    public class SByteSerializer : PrimitiveSerializer<sbyte> {
        public override object FromString(System.Type fieldType, string value) => sbyte.Parse(value);
    }

    public class ByteSerializer : PrimitiveSerializer<byte> {
        public override object FromString(System.Type fieldType, string value) => byte.Parse(value);
    }

    public class BoolSerializer : PrimitiveSerializer<bool> {
        public override object FromString(System.Type fieldType, string value) => bool.Parse(value);
    }

    public class Float2Serializer : SerializerBase<Unity.Mathematics.float2> {

        public override object FromString(System.Type fieldType, string value) {
            var splitted = (value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], out float x);
            float.TryParse(splitted[1], out float y);
            return new Unity.Mathematics.float2(x, y);
        }
        
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (Unity.Mathematics.float2)obj;
            builder.Append('"');
            builder.Append(val.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('"');
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    public class Float3Serializer : SerializerBase<Unity.Mathematics.float3> {
        
        public override object FromString(System.Type fieldType, string value) {
            var splitted = ((string)value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], out float x);
            float.TryParse(splitted[1], out float y);
            float.TryParse(splitted[2], out float z);
            return new Unity.Mathematics.float3(x, y, z);
        }

        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (Unity.Mathematics.float3)obj;
            builder.Append('"');
            builder.Append(val.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('"');
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    public class Float4Serializer : SerializerBase<Unity.Mathematics.float4> {
        
        public override object FromString(System.Type fieldType, string value) {
            var splitted = (value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], out float x);
            float.TryParse(splitted[1], out float y);
            float.TryParse(splitted[2], out float z);
            float.TryParse(splitted[3], out float w);
            return new Unity.Mathematics.float4(x, y, z, w);
        }

        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (Unity.Mathematics.float4)obj;
            builder.Append('"');
            builder.Append(val.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.w.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('"');
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    public class QuaternionSerializer : SerializerBase<Unity.Mathematics.quaternion> {
        
        public override object FromString(System.Type fieldType, string value) {
            var splitted = ((string)value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], out float x);
            float.TryParse(splitted[1], out float y);
            float.TryParse(splitted[2], out float z);
            return Unity.Mathematics.quaternion.Euler(x, y, z);
        }

        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (Unity.Mathematics.quaternion)obj;
            var euler = val.ToEuler();
            builder.Append('"');
            builder.Append(euler.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(euler.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(euler.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('"');
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

}