using System;
using System.Collections;
using System.Diagnostics;

namespace Amundsen
{
  /// <summary>
  ///  MIME-Type Parser
  /// 
  /// This module provides basic functions for handling mime-types. It can handle
  /// matching mime-types against a list of media-ranges. See section 14.1 of 
  /// the HTTP specification [RFC 2616] for a complete explanation.
  /// 
  /// http://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html#sec14.1
  /// 
  /// Contents:
  /// - ParseMimeType()   : Parses a mime-type into its component parts.
  /// - ParseMediaRange() : Media-ranges are mime-types with wild-cards and a 'q' quality parameter.
  /// - Quality()         : Determines the quality ('q') of a mime-type when compared against a list of media-ranges.
  /// - QualityParsed()   : Just like quality() except the second parameter must be pre-parsed.
  /// - BestMatch()       : Choose the mime-type with the highest quality ('q') from a list of candidates.
  /// 
  /// version : 0.1 (2009-02-10) 
  /// author  : Mike Amundsen
  /// email   : mamund@yahoo.com
  /// credits : Joe Gregorio (joe@bitworking.org)
  /// notes   : This is a C# port of the original mimeparser (see credits)
  /// </summary>
  public class MimeParse
  {
    private MimeParse()
    {
      // hide constructor
    }

    #region Public Methods
    /// <summary>
    ///  Takes a list of supported mime-types and finds the best
    ///  match for all the media-ranges listed in header. The value of
    ///  header must be a string that conforms to the format of the 
    ///  HTTP Accept: header. The value of 'supported' is a list of
    ///  mime-types.
    ///  
    /// >>> best_match(['application/xbel+xml', 'text/xml'], 'text/*;q=0.5,*/*; q=0.1')
    /// 'text/xml'
    /// </summary>
    /// <param name="supported"></param>
    /// <param name="header"></param>
    /// <returns></returns>
    public static string BestMatch(string[] supported, string header)
    {
      ParseResultsCollection parseColl = new ParseResultsCollection();
      FitnessAndQualityCollection weightedMatches = new FitnessAndQualityCollection();

      string[] r = header.Split(',');
      for (int i = 0; i < r.Length; i++)
      {
        parseColl.Add(ParseMediaRange(r[i]));
      }

      foreach (string s in supported)
      {
        FitnessAndQuality faq = FitnessAndQualityParsed(s, parseColl);
        faq.MimeType = s;
        weightedMatches.Add(faq);
      }

      weightedMatches.Sort();
      FitnessAndQuality last = weightedMatches.Last(); ;

      return (last.Quality != 0 ? last.MimeType : string.Empty);
    }

    /// <summary>
    ///  Returns the quality 'q' of a mime-type when compared
    ///  against the media-ranges in ranges. For example:
    /// 
    ///  >>> quality('text/html','text/*;q=0.3, text/html;q=0.7, text/html;level=1, text/html;level=2;q=0.4, */*;q=0.5')
    ///  0.7
    /// </summary>
    /// <param name="MimeType"></param>
    /// <param name="ranges"></param>
    /// <returns></returns>
    public static double Quality(string MimeType, string ranges)
    {
      ParseResultsCollection parseColl = new ParseResultsCollection();
      foreach (string r in ranges.Split(','))
      {
        parseColl.Add(ParseMediaRange(r));
      }
      return QualityParsed(MimeType, parseColl);
    }
    #endregion

    #region Protected Methods
    /// <summary>
    ///  Carves up a media range and returns a tuple of the
    ///  (type, subtype, params) where 'params' is a dictionary
    ///  of all the parameters for the media range.
    ///  For example, the media range 'application/*;q=0.5' would
    ///  get parsed into:
    ///
    ///  ('application', '*', {'q', '0.5'})
    ///
    ///  In addition this function also guarantees that there 
    ///  is a value for 'q' in the params dictionary, filling it
    ///  in with a proper default if necessary. 
    /// </summary>
    /// <param name="range"></param>
    /// <returns></returns>
    protected static ParseResults ParseMediaRange(string range)
    {
      ParseResults results = ParseMimeType(range);
      string q = (results.Parameters["q"] != null ? results.Parameters["q"].ToString() : "1");
      double d = Convert.ToDouble((q!=string.Empty?q:"1"));
      q = ((d < 0 || d > 1) ? "1" : q);

      if (results.Parameters.Contains("q"))
      {
        results.Parameters["q"] = q;
      }
      else
      {
        results.Parameters.Add("q", q);
      }

      return results;
    }

    /// <summary>
    ///  Carves up a mime-type and returns a tuple of the
    ///  (type, subtype, params) where 'params' is a dictionary
    ///  of all the parameters for the media range.
    ///  For example, the media range 'application/xhtml;q=0.5' would
    ///  get parsed into:
    ///
    ///  ('application', 'xhtml', {'q', '0.5'})    
    /// </summary>
    /// <param name="mimeType"></param>
    /// <returns></returns>
    protected static ParseResults ParseMimeType(string mimeType)
    {
      string[] parts = mimeType.Split(';');
      ParseResults results = new ParseResults();
      results.Parameters = new Hashtable();

      for (int i = 0; i < parts.Length; i++)
      {
        string p = parts[i];
        string[] subParts = p.Split('=');
        if (subParts.Length == 2)
        {
          results.Parameters.Add(subParts[0].Trim(), subParts[1].Trim());
        }
      }

      string fullType = parts[0].Trim();
      if (fullType == "*")
      {
        fullType = "*/*";
      }

      string[] types = fullType.Split('/');
      results.Type = types[0].Trim();
      results.SubType = types[1].Trim();

      return results;
    }

    /// <summary>
    ///  Find the best match for a given mime-type against 
    ///  a list of media_ranges that have already been 
    ///  parsed by parse_media_range(). Returns a tuple of
    ///  the fitness value and the value of the 'q' quality
    ///  parameter of the best match, or (-1, 0) if no match
    ///  was found. Just as for quality_parsed(), 'parsed_ranges'
    ///  must be a list of parsed media ranges.    
    /// </summary>
    /// <param name="mimeType"></param>
    /// <param name="parsedRanges"></param>
    /// <returns></returns>
    protected static FitnessAndQuality FitnessAndQualityParsed(string mimeType, ParseResultsCollection parsedRanges)
    {
      int bestFitness = -1;
      double bestFitQ = 0;
      ParseResults target = ParseMediaRange(mimeType);

      foreach (ParseResults parsed in parsedRanges)
      {
        if (
            (target.Type == parsed.Type || parsed.Type == "*" || target.Type == "*")
            &&
            (target.SubType == parsed.SubType || parsed.SubType == "*" || target.SubType == "*")
          )
        {
          int paramMatches = 0;
          foreach (string k in target.Parameters.Keys)
          {
            if (k != "q" && parsed.Parameters.ContainsKey(k) && (string)target.Parameters[k] == (string)parsed.Parameters[k])
            {
              paramMatches++;
            }

            int fitness = (parsed.Type == target.Type ? 100 : 0);
            fitness += (parsed.SubType == target.SubType ? 10 : 0);
            fitness += paramMatches;

            if (fitness > bestFitness)
            {
              bestFitness = fitness;
              bestFitQ = Convert.ToDouble(parsed.Parameters["q"]);
            }
          }
        }
      }

      return new FitnessAndQuality(bestFitness, bestFitQ);
    }

    /// <summary>
    ///  Find the best match for a given mime-type against
    ///  a list of media_ranges that have already been
    ///  parsed by parse_media_range(). Returns the
    ///  'q' quality parameter of the best match, 0 if no
    ///  match was found. This function bahaves the same as quality()
    ///  except that 'parsed_ranges' must be a list of
    ///  parsed media ranges.
    /// </summary>
    /// <param name="mimeType"></param>
    /// <param name="parseColl"></param>
    /// <returns></returns>
    protected static double QualityParsed(string mimeType, ParseResultsCollection parseColl)
    {
      return FitnessAndQualityParsed(mimeType, parseColl).Quality;
    }
    #endregion

    #region Container Classes
    /// <summary>
    ///  ParseResults holds the results of parsing a MIME-type. 
    /// </summary>
    protected class ParseResults
    {
      private Hashtable _parameters = new Hashtable();
      private string _type = string.Empty;
      private string _subType = string.Empty;

      /// <summary>
      /// Additional MIME parameters
      /// </summary>
      public Hashtable Parameters
      {
        get { return _parameters; }
        set { _parameters = value; }
      }

      /// <summary>
      /// MIME type value
      /// </summary>
      public string Type
      {
        get { return _type; }
        set { _type = value; }
      }
      
      /// <summary>
      /// MIME subtype value
      /// </summary>
      public string SubType
      {
        get { return _subType; }
        set { _subType = value; }
      }

      /// <summary>
      /// Constructor
      /// </summary>
      public ParseResults() { }
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="type"></param>
      /// <param name="subType"></param>
      /// <param name="paramlist"></param>
      public ParseResults(string type, string subType, Hashtable paramlist)
      {
        this._type = type;
        this._subType = subType;
        this._parameters = paramlist;
      }

      /// <summary>
      /// 
      /// </summary>
      /// <returns></returns>
      public override int GetHashCode()
      {
        return base.GetHashCode();
      }
      /// <summary>
      /// Override the equals method
      /// </summary>
      /// <param name="obj"></param>
      /// <returns></returns>
      public override bool Equals(object obj)
      {
        bool rtn = false;
        ParseResults pr = (ParseResults)obj;

        if (pr._type == this._type && pr._subType == this._subType && pr._parameters.Count == this._parameters.Count)
        {
          foreach (string k in pr._parameters.Keys)
          {
            if ((string)pr._parameters[k] != (string)this._parameters[k])
            {
              break;
            }
          }
          rtn = true;
        }

        return rtn;
      }
    }
    /// <summary>
    /// ParseResults collection class
    /// </summary>
    protected class ParseResultsCollection : CollectionBase
    {
      /// <summary>
      /// Adds ParseResults objects to the collection
      /// </summary>
      /// <param name="parseResults"></param>
      public void Add(ParseResults parseResults)
      {
        this.List.Add(parseResults);
      }
    }

    /// <summary>
    /// Fitness &amp; Quality class
    /// </summary>
    protected class FitnessAndQuality : IComparable
    {
      private int _fitness;
      private double _quality;
      private string _mimeType;
      private int _order = 0;

      /// <summary>
      /// Read-Only value indicating the order of the items in the list
      /// </summary>
      public int Order
      {
        get { return _order; }
        set { _order = value; }
      }
      
      /// <summary>
      /// Fitness value
      /// </summary>
      public int Fitness
      {
        get { return _fitness; }
        set { _fitness = value; }
      }

      /// <summary>
      /// Quality value
      /// </summary>
      public double Quality
      {
        get { return _quality; }
        set { _quality = value; }
      }

      /// <summary>
      /// MIME-type value
      /// </summary>
      public string MimeType
      {
        get { return _mimeType; }
        set { _mimeType = value; }
      }

      /// <summary>
      /// Constructor
      /// </summary>
      public FitnessAndQuality() { }

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="fitness"></param>
      /// <param name="quality"></param>
      public FitnessAndQuality(int fitness, double quality)
      {
        this._fitness = fitness;
        this._quality = quality;
      }

      /// <summary>
      /// Handles sorting details
      /// </summary>
      /// <param name="obj"></param>
      /// <returns></returns>
      public int CompareTo(object obj)
      {
        int rtn = 0;
        FitnessAndQuality faq = (FitnessAndQuality)obj;

        if (this._fitness == faq._fitness)
        {
          if (this._quality == faq._quality)
          {
            //rtn = string.Compare(this._mimeType, faq._mimeType, true);
            rtn = (this._order < faq._order ? -1 : 1);
          }
          else
          {
            rtn = (this._quality < faq._quality ? -1 : 1);
          }
        }
        else
        {
          rtn = (this._fitness < faq._fitness ? -1 : 1);
        }

        return rtn;
      }
    }
    /// <summary>
    /// FitnessAndQuality collection class
    /// </summary>
    protected class FitnessAndQualityCollection : CollectionBase
    {
      /// <summary>
      /// Add a new FitnessAndQuality object to the collection
      /// </summary>
      /// <param name="faq"></param>
      public void Add(FitnessAndQuality faq)
      {
        faq.Order = this.Count;
        this.List.Add(faq);
      }

      /// <summary>
      /// Sort the collection
      /// </summary>
      public void Sort()
      {
        this.InnerList.Sort();
      }

      /// <summary>
      /// Return the last object in the collection
      /// </summary>
      /// <returns></returns>
      public FitnessAndQuality Last()
      {
        FitnessAndQuality faq = null;
        for (int i = 0; i < this.List.Count; i++)
        {
          faq = (FitnessAndQuality)this.List[i];
        }
        return faq;
      }
    }
    #endregion

    #region Testing
    /// <summary>
    /// Internal tests
    /// </summary>
    public static void Tests()
    {
      // test parseMediaRange
      Hashtable parms = new Hashtable();
      parms.Add("q", "1");
      Debug.Assert(new ParseResults("application", "xml", parms).Equals(MimeParse.ParseMediaRange("application/xml;q=1")));
      Debug.Assert(new ParseResults("application", "xml", parms).Equals(MimeParse.ParseMediaRange("application/xml")));
      Debug.Assert(new ParseResults("application", "xml", parms).Equals(MimeParse.ParseMediaRange("application/xml;q=")));
      Debug.Assert(new ParseResults("application", "xml", parms).Equals(MimeParse.ParseMediaRange("application/xml ; q=")));
      parms.Add("b","other");
      Debug.Assert(new ParseResults("application", "xml", parms).Equals(MimeParse.ParseMediaRange("application/xml ; q=1;b=other")));
      Debug.Assert(new ParseResults("application", "xml", parms).Equals(MimeParse.ParseMediaRange("application/xml ; q=2;b=other")));

      // Java URLConnection class sends an Accept header that includes a single *
      parms = new Hashtable();
      parms.Add("q",".2");
      Debug.Assert(new ParseResults("*", "*", parms).Equals(MimeParse.ParseMediaRange(" *; q=.2")));

      // test quality method
      string accept = "text/*;q=0.3, text/html;q=0.7, text/html;level=1, text/html;level=2;q=0.4, */*;q=0.5";
      Debug.Assert(MimeParse.Quality("text/html;level=1", accept) == 1);
      Debug.Assert(MimeParse.Quality("text/html", accept) == 0.7);
      Debug.Assert(MimeParse.Quality("text/plain", accept) == 0.3);
      Debug.Assert(MimeParse.Quality("image/jpeg", accept) == 0.5);
      Debug.Assert(MimeParse.Quality("text/html;level=2", accept) == 0.4);
      Debug.Assert(MimeParse.Quality("text/html;level=3", accept) == 0.7);

      // test BestMatch
      string[] mimeTypesSupported = new string[] { "application/xbel+xml", "application/xml" };
      // direct match
      Debug.Assert(MimeParse.BestMatch(mimeTypesSupported, "application/xbel+xml") == "application/xbel+xml");
      // direct match with a q parameter
      Debug.Assert(MimeParse.BestMatch(mimeTypesSupported, "application/xbel+xml; q=1") == "application/xbel+xml");

      // direct match of our second choice with a q parameter
      Debug.Assert(MimeParse.BestMatch(mimeTypesSupported, "application/xml; q=1") == "application/cxml");
      // match using a subtype wildcard
      Debug.Assert(MimeParse.BestMatch(mimeTypesSupported, "application/*; q=1") == "application/xml");
      // match using a type wildcard
      Debug.Assert(MimeParse.BestMatch(mimeTypesSupported, "*/*") == "application/xml");

      mimeTypesSupported = new string[] { "application/xbel+xml", "text/xml" };
      // match using a type versus a lower weighted subtype
      Debug.Assert(MimeParse.BestMatch(mimeTypesSupported, "text/*;q=0.5,*/*; q=0.1") == "text/xml");
      // fail to match anything
      Debug.Assert(MimeParse.BestMatch(mimeTypesSupported, "text/html,application/atom+xml; q=0.9") == string.Empty);

      // common AJAX scenario
      mimeTypesSupported = new string[] { "application/json", "text/html" };
      Debug.Assert(MimeParse.BestMatch(mimeTypesSupported, "application/json, text/javascript, */*") == "application/json");
      // verify fitness ordering
      Debug.Assert(MimeParse.BestMatch(mimeTypesSupported, "application/json, text/html;q=0.9") == "application/json");

      // wild cards
      mimeTypesSupported = new string[] { "image/*", "application/xml" };
      // match using a type wildcard
      Debug.Assert(MimeParse.BestMatch(mimeTypesSupported, "image/png") == "image/*");
      // match using a wildcard for both requested and supported
      Debug.Assert(MimeParse.BestMatch(mimeTypesSupported, "image/*") == "image/*");
    }
    #endregion
  }
}
