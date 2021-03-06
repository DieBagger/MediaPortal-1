/*
 * Oracle database schema analyzer
 * Copyright (C) 2004 Andreas Seibt
 * 
 * This library is free software; you can redistribute it and/or modify it 
 * under the terms of the GNU Lesser General Public License 2.1 or later, as
 * published by the Free Software Foundation. See the included License.txt
 * or http://www.gnu.org/copyleft/lesser.html for details.
 *
 * $Id: OracleODPAnalyzer.cs 1232 2008-03-14 05:36:00Z mm $
 */

using System;
using Gentle.Common;
using Gentle.Framework;
using Oracle.DataAccess.Client;

namespace Gentle.Provider.OracleODP
{
	/// <summary>
	/// This class is a caching database analyzer. When first created it will build a cache of
	/// all found tables and populate an ObjectMap with as much information as is available.
	/// </summary>
	public class OracleODPAnalyzer : GentleAnalyzer
	{
		public OracleODPAnalyzer( IGentleProvider provider ) : base( provider )
		{
		}

		public override ColumnInformation AnalyzerCapability
		{
			// ciBasic = ColumnInformation.Size | ColumnInformation.Type;
			// ciExtra = ColumnInformation.IsNullable | ColumnInformation.IsUnique;
			// ciKey = ColumnInformation.IsPrimaryKey | ColumnInformation.IsAutoGenerated;
			// ciRelationOut = ColumnInformation.IsForeignKey;
			// ciRelationIn = ColumnInformation.HasForeignKey;
			// ciLocal = ciBasic | ciExtra | ciKey | ciRelationOut;
			// ciAll = ciLocal | ciRelationIn;
			get { return ColumnInformation.ciLocal & ~ColumnInformation.IsAutoGenerated; }
		}

		private const string ORA_VERSION_SELECT =
			@"select VERSION ""Version"" from PRODUCT_COMPONENT_VERSION where lower(product) like ('%oracle%')";
		// this was old old version (doesn't work on Personal edition):
		//		@"select VERSION ""Version"" from PRODUCT_COMPONENT_VERSION where PRODUCT like '%Oracle%'";
		// this doesn't work on Personal edition:
		//		@"select VERSION ""Version"" from PRODUCT_COMPONENT_VERSION where SUBSTR(PRODUCT,1,6)='Oracle'";
		// this requires admin privileges:
		//		@"select VERSION ""Version"" from V$INSTANCE"; 

		private const string ORA9_SELECT =
			@"select TAB.TABLE_NAME       ""TableName"",  " +
			@"		   TAB.COLUMN_NAME      ""ColumnName"",  " +
			@"		   TAB.DATA_TYPE        ""Type"",  " +
			@"		   TAB.DATA_LENGTH      ""Size"",  " +
			@"		   TAB.NULLABLE         ""IsNullable"",  " +
			@"		   TAB.DATA_DEFAULT     ""DefaultValue"",  " +
			@"		   CO.CONSTRAINT_NAME   ""ConstraintName"", " +
			@"		   CO.CONSTRAINT_TYPE   ""ConstraintType"", " +
			@"		   CO.R_CONSTRAINT_NAME ""ConstraintReference"", " +
			@"		   CO.DELETE_RULE       ""DeleteRule"" " +
			@"from USER_TAB_COLUMNS TAB LEFT OUTER JOIN " +
			@"	     (USER_CONSTRAINTS CO INNER JOIN USER_CONS_COLUMNS CO1 ON " +
			@"		   CO.TABLE_NAME = CO1.TABLE_NAME AND CO.CONSTRAINT_NAME = CO1.CONSTRAINT_NAME) ON " +
			@"	   TAB.TABLE_NAME = CO.TABLE_NAME AND TAB.COLUMN_NAME = CO1.COLUMN_NAME ";

		private const string SELECT_SINGLE = " WHERE TAB.TABLE_NAME = '{0}' ";

		private const string ORDER_BY = @"ORDER BY TAB.TABLE_NAME, TAB.COLUMN_NAME, CO1.CONSTRAINT_NAME";

		private const string ORA8_SELECT =
			@"select TAB.TABLE_NAME       ""TableName"", " +
			@"       TAB.COLUMN_NAME      ""ColumnName"", " +
			@"       TAB.DATA_TYPE        ""Type"", " +
			@"       TAB.DATA_LENGTH      ""Size"", " +
			@"       TAB.NULLABLE         ""IsNullable"", " +
			@"       TAB.DATA_DEFAULT     ""DefaultValue"", " +
			@"       CO.CONSTRAINT_NAME   ""ConstraintName"", " +
			@"       CO.CONSTRAINT_TYPE   ""ConstraintType"", " +
			@"       CO.R_CONSTRAINT_NAME ""ConstraintReference"", " +
			@"       CO.DELETE_RULE       ""DeleteRule"" " +
			@"from USER_TAB_COLUMNS TAB, USER_CONSTRAINTS CO, USER_CONS_COLUMNS CO1 " +
			@"where (TAB.TABLE_NAME = CO1.TABLE_NAME(+)) and " +
			@"      (TAB.COLUMN_NAME = CO1.COLUMN_NAME(+)) and " +
			@"      (CO1.constraint_name = CO.constraint_name(+)) and " +
			@"      (CO1.TABLE_NAME = CO.TABLE_NAME(+)) ";

		private const string ORA9_SELECT_REFERENCES =
			@"select CO.TABLE_NAME        ""ParentTable"", " +
			@"		   CO1.COLUMN_NAME      ""ParentColumn"", " +
			@"		   CO.CONSTRAINT_NAME   ""ConstraintName"", " +
			@"		   CO.R_CONSTRAINT_NAME ""ConstraintReference"", " +
			@"		   IDX2.TABLE_NAME      ""ChildTable"", " +
			@"		   IDX2.COLUMN_NAME     ""ChildColumn"" " +
			@"from (USER_CONSTRAINTS CO INNER JOIN USER_CONS_COLUMNS CO1 ON " +
			@"			   CO.TABLE_NAME = CO1.TABLE_NAME AND CO.CONSTRAINT_NAME = CO1.CONSTRAINT_NAME) " +
			@"		 LEFT OUTER JOIN USER_IND_COLUMNS IDX2 ON " +
			@"			  CO.R_CONSTRAINT_NAME = IDX2.INDEX_NAME AND CO1.POSITION = IDX2.COLUMN_POSITION " +
			@"where CO.CONSTRAINT_TYPE = 'R' AND " +
			@"      CO.R_CONSTRAINT_NAME = '{0}'";

		private const string ORA8_SELECT_REFERENCES =
			@"select CO.TABLE_NAME        ""ParentTable"", " +
			@"       CO1.COLUMN_NAME      ""ParentColumn"", " +
			@"       CO.CONSTRAINT_NAME   ""ConstraintName"", " +
			@"       CO.R_CONSTRAINT_NAME ""ConstraintReference"", " +
			@"       IDX2.TABLE_NAME      ""ChildTable"", " +
			@"       IDX2.COLUMN_NAME     ""ChildColumn"" " +
			@"from USER_CONSTRAINTS CO, USER_CONS_COLUMNS CO1, USER_IND_COLUMNS IDX2 " +
			@"where CO.TABLE_NAME = CO1.TABLE_NAME AND " +
			@"      CO.CONSTRAINT_NAME = CO1.CONSTRAINT_NAME AND " +
			@"      CO.R_CONSTRAINT_NAME = IDX2.INDEX_NAME AND " +
			@"      CO1.POSITION = IDX2.COLUMN_POSITION AND " +
			@"      CO.CONSTRAINT_TYPE = 'R' AND " +
			@"      CO.R_CONSTRAINT_NAME = '{0}'";

		// This query is constructed from above by unfolding and optimizing views
		// because Oracle 8.1.6 executes query ORA8_SELECT_REFERENCES extremely poor 
		// ORA816_SELECT_REFERENCES query is tested only with Oracle 8.1.6
		private const string ORA816_SELECT_REFERENCES =
			@"select O.NAME ""ParentTable"", " +
			@"	 COL.NAME ""ParentColumn"", " +
			@"	 OC.NAME ""ConstraintName"", " +
			@"	 RC.NAME ""ConstraintReference"", " +
			@"	 BASE.NAME  ""ChildTable"", " +
			@"	 IDC.NAME ""ChildColumn""	" +
			@"from SYS.CON$ OC, SYS.CON$ RC, SYS.CDEF$ C, SYS.OBJ$ O, SYS.COL$ COL, " +
			@"     SYS.CCOL$ CC, SYS.OBJ$ IDX, SYS.IND$ I, SYS.ICOL$ IC, SYS.OBJ$ BASE, SYS.COL$ IDC " +
			@"where OC.CON# = C.CON# and C.RCON# = RC.CON# and C.TYPE# = 4 and C.OBJ# = O.OBJ# " +
			@"	and C.CON# = CC.CON# and CC.OBJ# = COL.OBJ# and CC.INTCOL# = COL.INTCOL# " +
			@"	and IDX.OWNER# = O.OWNER# and RC.NAME = IDX.NAME and IDX.OBJ# = I.OBJ# " +
			@"	and I.TYPE# IN (1, 2, 3, 4, 6, 7, 9) and IC.OBJ# = IDX.OBJ# and IC.BO# = BASE.OBJ# " +
			@"  and IDC.OBJ# = BASE.OBJ# and IC.INTCOL# = IDC.INTCOL# and O.OWNER# = USERENV('SCHEMAID') " +
			@"  and RC.NAME =  '{0}' ";

		private static bool GetBoolean( string boolean )
		{
			string[] valids = new[] { "yes", "true", "1", "y" };
			boolean = boolean == null ? "false" : boolean.ToLower();
			bool result = false;
			foreach( string valid in valids )
			{
				result |= valid.Equals( boolean );
			}
			return result;
		}

		/// <summary>
		/// Please refer to the <see cref="GentleAnalyzer"/> class and the <see cref="IDatabaseAnalyzer"/> 
		/// interface it implements a description of this method.
		/// </summary>
		public override void Analyze( string tableName )
		{
			try
			{
				bool isSingleRun = tableName != null;
				string selectSingle = isSingleRun ? String.Format( SELECT_SINGLE, tableName ) : String.Empty;

				// Check Oracle version and select appropriate SQL-Syntax
				SqlResult sr = broker.Execute( ORA_VERSION_SELECT );
				//				string ver = sr.GetString( 0, "Version" ).Substring( 0, 1 );
				//				int version = Convert.ToInt32( sr.GetString( 0, "Version" ).Substring( 0, 1 ) );

				string ver = sr.GetString( 0, "Version" );

				int indexOfDot = ver.IndexOf( "." );
				if( indexOfDot < 0 )
				{
					throw new GentleException( Error.DeveloperError, "Unable to determine Oracle database version." );
				}

				int version = Convert.ToInt32( ver.Substring( 0, indexOfDot ) );

				string select;
				string selectReferences;
				if( version < 9 )
				{
					// If Oracle version == '8.1.6' use no-views selectReferences
					if( ver.Substring( 0, 5 ).CompareTo( "8.1.6" ) == 0 )
					{
						selectReferences = ORA816_SELECT_REFERENCES;
					}
					else
					{
						selectReferences = ORA8_SELECT_REFERENCES;
					}
					select = ORA8_SELECT + selectSingle + ORDER_BY;
				}
				else
				{
					select = ORA9_SELECT + selectSingle + ORDER_BY;
					selectReferences = ORA9_SELECT_REFERENCES;
				}

				sr = broker.Execute( select );
				// process result set using columns:
				// TableName, ColumnName, Type, Size, IsNullable, DefaultValue, 
				// ConstraintName, ConstraintReference, ConstraintType, UpdateRule, DeleteRule
				for( int i = 0; i < sr.Rows.Count; i++ )
				{
					try
					{
						string dbTableName = sr.GetString( i, "TableName" );
						if( ! isSingleRun || tableName.ToLower().Equals( dbTableName.ToLower() ) )
						{
							// get or create TableMap for table 
							TableMap map = GetTableMap( dbTableName );
							if( map == null )
							{
								map = new TableMap( provider, dbTableName );
								maps[ dbTableName.ToLower() ] = map;
							}
							// get or create FieldMap for column
							string columnName = sr.GetString( i, "ColumnName" );
							FieldMap fm = map.GetFieldMapFromColumn( columnName );
							if( fm == null )
							{
								fm = new FieldMap( map, columnName );
								map.Fields.Add( fm );
							}
							// get basic column information
							fm.SetDbType( sr.GetString( i, "Type" ), false );
							fm.SetIsNullable( GetBoolean( sr.GetString( i, "IsNullable" ) ) );

							if( sr[ i, "Size" ] != null )
							{
								if( fm.DbType == (long) OracleDbType.Clob )
								{
									//Max 4GB
									//Preferred size 4294967296
									fm.SetSize( int.MaxValue );
								}
								else
								{
									fm.SetSize( sr.GetInt( i, "Size" ) );
								}
							}

							// get column constraint infomation
							if( sr[ i, "ConstraintName" ] != null )
							{
								string typ = sr.GetString( i, "ConstraintType" );
								if( typ.ToLower().Equals( "p" ) )
								{
									fm.SetIsPrimaryKey( true );
								}
								else if( typ.ToLower().Equals( "r" ) )
								{
									string conref = sr.GetString( i, "ConstraintReference" );
									SqlResult res = broker.Execute( String.Format( selectReferences, conref ) );
									fm.SetForeignKeyTableName( res.GetString( 0, "ChildTable" ) );
									fm.SetForeignKeyColumnName( res.GetString( 0, "ChildColumn" ) );
								}
							}
						}
					}
					catch( GentleException fe )
					{
						// ignore errors caused by tables found in db but for which no map exists
						// TODO this should be a config option
						if( fe.Error != Error.NoObjectMapForTable )
						{
							throw;
						}
					}
				}
			}
			catch( Exception e )
			{
				Check.LogInfo( LogCategories.General, "Using provider {0} and connectionString {1}.",
				               provider.Name, provider.ConnectionString );
				Check.Fail( e, Error.Unspecified, "An error occurred while analyzing the database schema." );
			}
		}
	}
}