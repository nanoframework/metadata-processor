//
// Copyright (c) 2019 The nanoFramework project contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

namespace nanoFramework.Tools.MetadataProcessor
{
    internal enum nanoCLR_DataType : byte
    {
        // these where defined @ enum CLR_DataType

        DATATYPE_VOID, // 0 bytes

        DATATYPE_BOOLEAN, // 1 byte
        DATATYPE_I1, // 1 byte
        DATATYPE_U1, // 1 byte

        DATATYPE_CHAR, // 2 bytes
        DATATYPE_I2, // 2 bytes
        DATATYPE_U2, // 2 bytes

        DATATYPE_I4, // 4 bytes
        DATATYPE_U4, // 4 bytes
        DATATYPE_R4, // 4 bytes

        DATATYPE_I8, // 8 bytes
        DATATYPE_U8, // 8 bytes
        DATATYPE_R8, // 8 bytes
        DATATYPE_DATETIME, // 8 bytes     // Shortcut for System.DateTime
        DATATYPE_TIMESPAN, // 8 bytes     // Shortcut for System.TimeSpan
        DATATYPE_STRING,

        DATATYPE_OBJECT, // Shortcut for System.Object
        DATATYPE_CLASS, // CLASS <class Token>
        DATATYPE_VALUETYPE, // VALUETYPE <class Token>
        DATATYPE_SZARRAY, // Shortcut for single dimension zero lower bound array SZARRAY <type>
        DATATYPE_BYREF, // BYREF <type>
    }
}
