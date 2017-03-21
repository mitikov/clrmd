﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime
{
    
    /// <summary>
    /// Represents an object in the target process.
    /// </summary>
    [DebuggerDisplay("Address={HexAddress}, Type={Type.Name}")]
    public struct ClrObject : IEquatable<ClrObject>
    {
        private ulong _address;
        private ClrType _type;

        internal static ClrObject Create(ulong address, ClrType type)
        {
            ClrObject obj = new ClrObject()
            {
                _address = address,
                _type = type
            };

            return obj;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="address">The address of the object</param>
        /// <param name="type">The concrete type of the object.</param>
        public ClrObject(ulong address, ClrType type)
        {
            _address = address;
            _type = type;

            Debug.Assert(type != null);
            Debug.Assert(address == 0 || type.Heap.GetObjectType(address) == type);
        }

        /// <summary>
        /// The address of the object.
        /// </summary>
        public ulong Address { get { return _address; } }

        /// <summary>
        /// The address of the object in Hex format.
        /// </summary>
        public string HexAddress { get { return _address.ToString("x"); } }

        /// <summary>
        /// The type of the object.
        /// </summary>
        public ClrType Type { get { return _type; } }

        /// <summary>
        /// Returns if the object value is null.
        /// </summary>
        public bool IsNull { get { return _address == 0; } }

        /// <summary>
        /// Gets the size of the object.
        /// </summary>
        public ulong Size { get { return _type.GetSize(Address); } }

        /// <summary>
        /// Returns whether this object is actually a boxed primitive or struct.
        /// </summary>
        public bool IsBoxed { get { return !_type.IsObjectReference; } }

        /// <summary>
        /// Returns whether this object is an array or not.
        /// </summary>
        public bool IsArray { get { return _type.IsArray; } }

        /// <summary>
        /// Returns the count of elements in this array, or throws InvalidOperatonException if this object is not an array.
        /// </summary>
        public int Length
        {
            get
            {
                if (!IsArray)
                    throw new InvalidOperationException();

                return _type.GetArrayLength(Address);
            }
        }

        #region GetField
        /// <summary>
        /// Gets the given object reference field from this ClrObject.  Throws ArgumentException if the given field does
        /// not exist in the object.  Throws NullReferenceException if IsNull is true.
        /// </summary>
        /// <param name="fieldName">The name of the field to retrieve.</param>
        /// <returns>A ClrObject of the given field.</returns>
        public ClrObject GetObjectField(string fieldName)
        {
            if (IsNull)
                throw new NullReferenceException();

            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            if (!field.IsObjectReference)
                throw new ArgumentException($"Field '{_type.Name}.{fieldName}' is not an object reference.");

            ClrHeap heap = _type.Heap;

            ulong addr = field.GetAddress(_address);
            if (!heap.ReadPointer(addr, out ulong obj))
                throw new MemoryReadException(addr);

            ClrType type = heap.GetObjectType(obj);
            return new ClrObject(obj, type);
        }

        /// <summary>
        /// Gets the value of a primitive field.  This will throw an InvalidCastException if the type parameter
        /// does not match the field's type.
        /// </summary>
        /// <typeparam name="T">The type of the field itself.</typeparam>
        /// <param name="fieldName">The name of the field.</param>
        /// <returns>The value of this field.</returns>
        public T GetField<T>(string fieldName) where T : struct
        {
            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            object value = field.GetValue(_address);
            return (T)value;
        }

        /// <summary>
        /// Gets a string field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
        public string GetStringField(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.String, "string");
            RuntimeBase runtime = (RuntimeBase)_type.Heap.Runtime;

            if (!runtime.ReadPointer(address, out ulong str))
                throw new MemoryReadException(address);

            if (!runtime.ReadString(str, out string result))
                throw new MemoryReadException(str);

            return result;
        }

        private ulong GetFieldAddress(string fieldName, ClrElementType element, string typeName)
        {
            if (IsNull)
                throw new NullReferenceException();

            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            if (field.ElementType != element)
                throw new InvalidOperationException($"Field '{_type.Name}.{fieldName}' is not of type '{typeName}'.");

            ulong address = field.GetAddress(Address);
            return address;
        }
        #endregion

        #region IEquatable<ClrObject> 

        /// <summary>
        /// Determines if this instance and another specific <see cref="ClrObject"/> have the same value.
        /// <para>Instances are considered equal when they have same <see cref="Address"/>.</para>
        /// </summary>
        /// <param name="other">The <see cref="ClrObject"/> to compare to this instance.</param>
        /// <returns><c>true</c> if the <see cref="Address"/> of the parameter is same as <see cref="Address"/> in this instance; <c>false</c> otherwise.</returns>
        public bool Equals(ClrObject other)
        {
             return this.Address == other.Address;
        }
        #endregion

        #region Object overrides

        /// <summary>
        /// Determines whether this instance and a specified object, which must also be a <see cref="ClrObject"/>, have the same value.
        /// </summary>
        /// <param name="other">The <see cref="ClrObject"/> to compare to this instance.</param>
        /// <returns><c>true</c> if <paramref name="other"/> is <see cref="ClrObject"/>, and its <see cref="Address"/> is same as <see cref="Address"/> in this instance; <c>false</c> otherwise.</returns>
        public override bool Equals(object other)
        {
            if (other == null)
                return false;
            return (other is ClrObject) && this.Equals((ClrObject)other);
        }

        /// <summary>
        /// Returns the hash code for this <see cref="ClrObject"/> based on its <see cref="Address"/>.
        /// </summary>
        /// <returns>An <see cref="int"/> hash code for this instance.</returns>
        public override int GetHashCode()
        {
            return this.Address.GetHashCode();
        }
        #endregion

        #region Operators overloads

        /// <summary>
        /// Determines whether two specified <see cref="ClrObject"/> have the same value.
        /// </summary>
        /// <param name="left">First <see cref="ClrObject"/> to compare.</param>
        /// <param name="right">Second <see cref="ClrObject"/> to compare.</param>
        /// <returns><c>true</c> if <paramref name="left"/> <see cref="Equals(ClrObject)"/> <paramref name="right"/>; <c>false</c> otherwise.</returns>
        public static bool operator ==(ClrObject left, ClrObject right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two specified <see cref="ClrObject"/> have different values.
        /// </summary>
        /// <param name="left">First <see cref="ClrObject"/> to compare.</param>
        /// <param name="right">Second <see cref="ClrObject"/> to compare.</param>
        /// <returns><c>true</c> if the value of <paramref name="left"/> is different from the value of <paramref name="right"/>; <c>false</c> otherwise.</returns>
        public static bool operator !=(ClrObject left, ClrObject right)
        {
            return !left.Equals(right);
        }

        #endregion
    }
}