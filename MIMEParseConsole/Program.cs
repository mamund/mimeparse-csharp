using System;
using System.Text;
using System.Diagnostics;

namespace Amundsen
{
  /// <summary>
  /// Console test app for MimeParse
  /// 
  /// version : 0.1 (2009-02-10) 
  /// author  : Mike Amundsen
  /// email   : mamund@yahoo.com
  /// credits : Joe Gregorio (joe@bitworking.org)
  /// notes   : This is a C# port of the original mimeparser (see credits)
  /// </summary>
  class Program
  {
    static void Main(string[] args)
    {
      // show example results
      Console.WriteLine(MimeParse.BestMatch(new String[] { "application/xbel+xml", "text/xml" }, "text/*;q=0.5,*; q=0.1"));// 'text/xml'

      // run asserts
      MimeParse.Tests();

    }
  }
}
