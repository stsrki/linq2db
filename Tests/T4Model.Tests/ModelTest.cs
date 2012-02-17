﻿//---------------------------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated by T4Model template for T4 (https://github.com/igor-tkachev/t4models).
//    Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>
//---------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

using T4Model.Tests;

namespace T4Model.Tests
{
	public partial class TestClass1
	{
		#region Test Region

		[XmlArrayItem(typeof(int), DataType="List")                                                                 ] public int    Field1;
		[                                            XmlArray("Name1")                                              ] public string Field2;
		[XmlArrayItem(typeof(int), DataType="List"), XmlArray("Name21"), XmlArrayItem(typeof(char), DataType="List")] public string Field21;
		[XmlAttribute("Name1", typeof(int)),         XmlArray("N2")                                                 ] public string Field221 { get; set; }
		                                                                                                              public string Field2212;
		[XmlAttribute("Nm1", typeof(int))                                                                           ] public string Field23;

		#endregion

		#region Test Region 2

		public int    Field12;
		public string Field22;

		#endregion

		[XmlArrayItem(typeof(int), DataType="List")]
		public List<int> Field3; // Field3 comment

		[DisplayName("Prop")]
		public char Property1 { get; set; } // Property1 comment

		public List<int> Field31;

		public double Field5;

		public List<int> Field6;

		public double       Fld7;                           // Fld7
		public List<int>    Field8;
		public DateTime     FieldLongName;                  // field long name
		public List<string> Property2 { get;         set; } // Property2
		public List<int?>   Property3 { get; private set; } // Property3
		public int?         Prop1     { get;         set; } // Prop1

		public List<string> Field4;
	}

	[Serializable, DisplayName("TestClass")]
	public partial class TestClass2 : TestClass1
	{
	}
}