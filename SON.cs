///////////////////////////////////
// SON - Simple Object Notation. //
// 2015 Graham Hughes.           //
///////////////////////////////////
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace SONLib
{
  public class SONException : Exception { public SONException(string m) : base(m) {} }
  public class SON
  {
    #region externals
    #region defines
    public const char startChar = '(', addChar = ',', refChar = '@', quoteChar = '\"', endChar = ')';
    public static readonly string symbols = ""+startChar+addChar+refChar+quoteChar+endChar;
    public static readonly string fieldPattern = @"(?:^\s*([^"+symbols+@"]*)\s*$)";
    public static readonly string refPattern = @"(?:^\s*"+refChar+@"\s*([^"+symbols+@"]*)\s*$)";
    public static readonly string quotePattern = @"(?:^\s*("+quoteChar+@".*"+quoteChar+@")\s*$)";
    #endregion
    #region properties
    public SON this[int index] { get { if(!IsContainer) { UsageException(); } return list[index]; } }
    public SON this[string key] { get { if(!IsContainer) { UsageException(); } return map[key]; } }
    public SON Parent { get { return parent; } }
    public string Field { get { return field; } }
    public float Number { get { return float.Parse(field); } }
    public int Count { get { if(!IsContainer) { UsageException(); } return list.Count(); } }
    public bool IsContainer { get { return list != null; } }
    #endregion
    #region constructors
    public SON(string text) { int i = 0; this.Parse(text, ref i); }
    protected SON(string field, SON parent, bool container)
    {
      this.field = field;
      this.parent = parent;
      if(container) { InitialiseContainer(); }
    }
    #endregion
    #region methods
    ///////////////////////////////////////////////////////////
    //Parses the text and adds all the elements it finds to this SON
    public void Add(string text)
    {
      if(!IsContainer) { UsageException(); }
      int i = 0; this.Parse(text, ref i);
    }
    ///////////////////////////////////////////////////////////
    //Print the contents of the SON to string
    public string Stringify() { string s = ""; Stringify(ref s); return s; }

    ///////////////////////////////////////////////////////////
    //Finds the first child with "key", skips references.
    public SON FindDownard(string key)
    {
      try { return map[key]; }
      catch
      {
        if(IsContainer)
        {
          foreach(SON son in list)
          {
            if(son.Parent != this) { continue; }
            SON child = son.FindDownard(key);
            if(child != null) { return child; }
          }
        }
      }
      return null;
    }

    ///////////////////////////////////////////////////////////
    //Finds upwardly along chain of parents, but skips previous parents.
    public SON FindUpward(string key)
    {
      SON previous = this;
      for(SON parent = Parent; parent != null; parent = parent.Parent)
      {
        for(int i = 0; i < parent.Count; i++)
        {
          if(parent[i] == previous) { continue; }
          SON child = parent.FindDownard(key);
          if(child != null) { return child; }
        }
        previous = parent;
      }
      return null;
    }
    #endregion
    #endregion
    ///////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////
    #region internals
    #region exceptions
    ///////////////////////////////////////////////////////////
    //List of exceptions for internal use.
    protected void UsageException() { throw new SONException("You can't add to or access members of a field."); }
    protected void SyntaxException() { throw new SONException("Invalid syntax, check '"+addChar+"'s & '"+endChar+"'s"); }
    protected void ParentException() { throw new SONException("Added objects must be parented to this object."); }
    protected void KeyException(string key) { throw new SONException("Key already exists or is empty: '"+key+"'"); }
    protected void ReferenceException(string field) { throw new SONException("Reference not found or key taken: '"+field+"'"); }
    protected void InvalidFieldException(string field) { throw new SONException("Invalid Field: '"+field+"'"); }
    protected void TestKey(string key) { if(key == "" || map.ContainsKey(key)) { KeyException(key); } }
    #endregion
    #region members
    protected SON parent = null;
    protected List<SON> list = null;
    protected Dictionary<string, SON> map = null;
    protected string field = null;
    #endregion
    #region methods
    ///////////////////////////////////////////////////////////
    //Initialises this SON to be a container of SON's.
    protected void InitialiseContainer()
    {
      list = new List<SON>();
      map = new Dictionary<string, SON>();
    }

    ///////////////////////////////////////////////////////////
    //Adds a SON to this SON.
    protected SON AddSON(string field) { return AddSON(field, false); }
    protected SON AddSON(string field, bool container)
    {
      if(!IsContainer) { InitialiseContainer(); }
      else if(field != null && container) { TestKey(field); }
      SON son = new SON(field, this, container);
      list.Add(son);
      if(field != null && container) { map[field] = son; }
      return son;
    }

    ///////////////////////////////////////////////////////////
    //Parses the SON format
    protected void Parse(string text, ref int index)
    {
      string field = "";
      bool quoted = false;
      bool parsedChild = false;
      Action ParseChild = () => {} ;
      //Begin Parsing text! //////////////
      for( ; index < text.Length; index++)
      {
        char character = text[index];
        if(character == quoteChar) { quoted = !quoted; }
        if(!quoted && character == startChar)
        {
          //Begin a new parsedChild!
          //If the previous element was a parsedChild, then we shouldn't start a new one.
          if(parsedChild) { SyntaxException(); }
          index++;
          field = field.Trim();
          SON newSon = null;

          //Add the newSon to this son, it will be filled later as a container.
          if(field == "") { newSon = AddSON(null, true); }
          else if(RegexStrip(ref field, fieldPattern)) { newSon = AddSON(field, true); }
          else { InvalidFieldException(field); }

          //Pass the current index to parse later & skip over this text.
          int passedIndex = index;
          ParseChild += () => { newSon.Parse(text, ref passedIndex); };

          //NOTE: Ideally I'd like a better way to skip over a parsedChild.
          int depth = 1;
          for( ; index < text.Length && depth > 0; index++)
          {
            if(text[index] == quoteChar) { quoted = !quoted; }
            if(!quoted && text[index] == startChar) { depth++; }
            else if(!quoted && text[index] == endChar) { depth--; }
          }

          //Make sure the syntax of the parsed element is correct.
          if(depth != 0) { SyntaxException(); }
          index--;
          parsedChild = true;
          field = "";
        }
        //Add an element to this object
        else if(!quoted && (character == endChar || character == addChar))
        {
          //making sure we don't have too many closing braces and that we don't have extraneous characcters.
          if((character == endChar && parent == null) || (parsedChild && field.Trim() != "")) { SyntaxException(); }

          //If a child hasn't been parsed, add the field.
          if(parsedChild) { parsedChild = false; }
          else if(character == addChar) { AddField(field);  field = ""; }
          //We only want to add if there's something there in the case of an end character
          else if(index-field.Length <= 0 || text[index-field.Length-1] == addChar || field.Trim() != "")
          { AddField(field);  field = ""; }

          //If this is an ending character, parse elements and return.
          if(character == endChar) { ParseChild(); return; }
        }
        else { field += character; continue; }
      }
      ParseChild();
      //This adds any trailing members. Kind of an exception
      if(index-field.Length <= 0 || text[index-field.Length-1] == addChar) { AddField(field); }
    }

    ///////////////////////////////////////////////////////////
    //Add a field making sure it's syntax is correct
    protected void AddField(string field)
    {
      if(!IsContainer) { InitialiseContainer(); }
      if(field != "")
      {
        if(RegexStrip(ref field, quotePattern)) { AddSON(field); }
        else if(RegexStrip(ref field, fieldPattern)) { AddSON(field); }
        else if(RegexStrip(ref field, refPattern))
        {
          TestKey(field);
          SON reference = FindUpward(field);
          if(reference != null)
          {
            map[field] = reference;
            list.Add(reference);
          }
          else { ReferenceException(field); }
        } else { InvalidFieldException(field); } //Bad Field!!
      }
      else { AddSON(null); }
    }

    ///////////////////////////////////////////////////////////
    //This strips all characters except the first capture group.
    protected static bool RegexStrip(ref string field, string regex)
    {
      Match match = Regex.Match(field, regex);
      if(match.Success) { field = match.Groups[1].Captures[0].Value; }
      else { return false; }
      return true;
    }

    ///////////////////////////////////////////////////////////
    //Recursively turns fields into strings.
    protected void Stringify(ref string text)
    {
      text += field;
      if(IsContainer)
      {
        if(parent != null) { text += startChar; }
        foreach(SON son in list)
        {
          if(son.Parent == this) { son.Stringify(ref text); }
          else { text += refChar+son.Field; }
          if(son != list.Last()) { text += addChar; }
        }
        if(parent != null) { text += endChar; }
      }
    }
    #endregion
    #endregion
  }
}
