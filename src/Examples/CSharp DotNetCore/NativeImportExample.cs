﻿using System;
using System.Threading;
using libplctag.NativeImport;

namespace CSharpDotNetCore
{
    class NativeImportExample
    {
        public static void Run()
        {

            var tagHandle = plctag.plc_tag_create("protocol=ab_eip&gateway=192.168.0.10&path=1,0&cpu=LGX&elem_size=4&elem_count=1&name=MY_DINT", 1000);

            while (plctag.plc_tag_status(tagHandle) == 1)
            {
                Thread.Sleep(100);
            }
            var statusBeforeRead = plctag.plc_tag_status(tagHandle);
            if (statusBeforeRead != 0)
            {
                Console.WriteLine($"Something went wrong {statusBeforeRead}");
            }

            plctag.plc_tag_read(tagHandle, 1000);
            while (plctag.plc_tag_status(tagHandle) == 1)
            {
                Thread.Sleep(100);
            }
            var statusAfterRead = plctag.plc_tag_status(tagHandle);
            if (statusAfterRead != 0)
            {
                Console.WriteLine($"Something went wrong {statusAfterRead}");
            }

            var theValue = plctag.plc_tag_get_uint32(tagHandle, 0);

            plctag.plc_tag_destroy(tagHandle);

            Console.WriteLine(theValue);
        }

        public static void MyLogger(int tag_id, int debug_level, string message)
        {
            Console.WriteLine($"Tag Id: {tag_id}    Debug Level: {debug_level}    Message: {message}");
        }
    }
}
