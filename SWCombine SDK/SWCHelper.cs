using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Json;

namespace SWCombine.SDK
{
    static class SWCHelper
    {
        /// <summary>
        /// Converts a stream of json into the specified type.
        /// </summary>
        /// <typeparam name="TValue">Type that the json represents.</typeparam>
        /// <param name="stream">A stream that contains JSON data.</param>
        /// <returns>Return an instance of TValue or null if JSON can not be convert to type.</returns>
        /// <remarks>
        /// In order for this to work class definitions require the DataContract attribute 
        /// and properties require DataMember attribute.
        /// </remarks>
        public static TValue JsonTo<TValue>(Stream stream) where TValue : class
        {
            try
            {
                var jsonSerializer = new DataContractJsonSerializer(typeof(TValue));
                object obj = jsonSerializer.ReadObject(stream);
                return obj as TValue;
            }
            catch (Exception)
            {
                return null;
            }            
        }
    }
}
