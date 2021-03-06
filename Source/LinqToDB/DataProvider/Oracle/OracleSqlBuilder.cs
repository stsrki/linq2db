﻿using System;
using System.Data;
using System.Linq;

namespace LinqToDB.DataProvider.Oracle
{
	using Common;
	using SqlQuery;
	using SqlProvider;
	using System.Text;

	class OracleSqlBuilder : BasicSqlBuilder
	{
		public OracleSqlBuilder(ISqlOptimizer sqlOptimizer, SqlProviderFlags sqlProviderFlags, ValueToSqlConverter valueToSqlConverter)
			: base(sqlOptimizer, sqlProviderFlags, valueToSqlConverter)
		{
		}

		protected override void BuildSelectClause(SelectQuery selectQuery)
		{
			if (selectQuery.From.Tables.Count == 0)
			{
				AppendIndent().Append("SELECT").AppendLine();
				BuildColumns(selectQuery);
				AppendIndent().Append("FROM SYS.DUAL").AppendLine();
			}
			else
				base.BuildSelectClause(selectQuery);
		}

		protected override void BuildGetIdentity(SelectQuery selectQuery)
		{
			var identityField = selectQuery.Insert.Into.GetIdentityField();

			if (identityField == null)
				throw new SqlException("Identity field must be defined for '{0}'.", selectQuery.Insert.Into.Name);

			AppendIndent().AppendLine("RETURNING ");
			AppendIndent().Append("\t");
			BuildExpression(identityField, false, true);
			StringBuilder.AppendLine(" INTO :IDENTITY_PARAMETER");
		}

		public override ISqlExpression GetIdentityExpression(SqlTable table)
		{
			if (!table.SequenceAttributes.IsNullOrEmpty())
			{
				var attr = GetSequenceNameAttribute(table, false);

				if (attr != null)
					return new SqlExpression(attr.SequenceName + ".nextval", Precedence.Primary);
			}

			return base.GetIdentityExpression(table);
		}

		private static void ConvertEmptyStringToNullIfNeeded(ISqlExpression expr)
		{
			var sqlParameter = expr as SqlParameter;
			var sqlValue     = expr as SqlValue;

			if (sqlParameter != null && sqlParameter.Value is string && sqlParameter.Value.ToString() == "")
				sqlParameter.Value = null;

			if (sqlValue != null && sqlValue.Value is string && sqlValue.Value.ToString() == "")
				sqlValue.Value = null;
		}

		protected override void BuildPredicate(ISqlPredicate predicate)
		{
			if (predicate.ElementType == QueryElementType.ExprExprPredicate)
			{
				var expr = (SqlPredicate.ExprExpr)predicate;
				if (expr.Operator == SqlPredicate.Operator.Equal ||
					expr.Operator == SqlPredicate.Operator.NotEqual)
				{
					ConvertEmptyStringToNullIfNeeded(expr.Expr1);
					ConvertEmptyStringToNullIfNeeded(expr.Expr2);
				}
			}
			base.BuildPredicate(predicate);
		}

		protected override bool BuildWhere(SelectQuery selectQuery)
		{
			return base.BuildWhere(selectQuery) || !NeedSkip(selectQuery) && NeedTake(selectQuery) &&
			       selectQuery.OrderBy.IsEmpty && selectQuery.Having.IsEmpty;
		}

		string _rowNumberAlias;

		protected override ISqlBuilder CreateSqlBuilder()
		{
			return new OracleSqlBuilder(SqlOptimizer, SqlProviderFlags, ValueToSqlConverter);
		}

		protected override void BuildSql()
		{
			var selectQuery = (SelectQuery) Statement;

			if (NeedSkip(selectQuery))
			{
				var aliases = GetTempAliases(2, "t");

				if (_rowNumberAlias == null)
					_rowNumberAlias = GetTempAliases(1, "rn")[0];

				AppendIndent().AppendFormat("SELECT {0}.*", aliases[1]).AppendLine();
				AppendIndent().Append("FROM").    AppendLine();
				AppendIndent().Append("(").       AppendLine();
				Indent++;

				AppendIndent().AppendFormat("SELECT {0}.*, ROWNUM as {1}", aliases[0], _rowNumberAlias).AppendLine();
				AppendIndent().Append("FROM").    AppendLine();
				AppendIndent().Append("(").       AppendLine();
				Indent++;

				base.BuildSql();

				Indent--;
				AppendIndent().Append(") ").Append(aliases[0]).AppendLine();

				if (NeedTake(selectQuery))
				{
					AppendIndent().AppendLine("WHERE");
					AppendIndent().Append("\tROWNUM <= ");
					BuildExpression(Add<int>(selectQuery.Select.SkipValue, selectQuery.Select.TakeValue));
					StringBuilder.AppendLine();
				}

				Indent--;
				AppendIndent().Append(") ").Append(aliases[1]).AppendLine();
				AppendIndent().Append("WHERE").AppendLine();

				Indent++;

				AppendIndent().AppendFormat("{0}.{1} > ", aliases[1], _rowNumberAlias);
				BuildExpression(selectQuery.Select.SkipValue);

				StringBuilder.AppendLine();
				Indent--;
			}
			else if (NeedTake(selectQuery) && (!selectQuery.OrderBy.IsEmpty || !selectQuery.Having.IsEmpty))
			{
				var aliases = GetTempAliases(1, "t");

				AppendIndent().AppendFormat("SELECT {0}.*", aliases[0]).AppendLine();
				AppendIndent().Append("FROM").    AppendLine();
				AppendIndent().Append("(").       AppendLine();
				Indent++;

				base.BuildSql();

				Indent--;
				AppendIndent().Append(") ").Append(aliases[0]).AppendLine();
				AppendIndent().Append("WHERE").AppendLine();

				Indent++;

				AppendIndent().Append("ROWNUM <= ");
				BuildExpression(selectQuery.Select.TakeValue);

				StringBuilder.AppendLine();
				Indent--;
			}
			else
			{
				base.BuildSql();
			}
		}

		protected override void BuildWhereSearchCondition(SelectQuery selectQuery, SqlSearchCondition condition)
		{
			if (NeedTake(selectQuery) && !NeedSkip(selectQuery) && selectQuery.OrderBy.IsEmpty && selectQuery.Having.IsEmpty)
			{
				BuildPredicate(
					Precedence.LogicalConjunction,
					new SqlPredicate.ExprExpr(
						new SqlExpression(null, "ROWNUM", Precedence.Primary),
						SqlPredicate.Operator.LessOrEqual,
						selectQuery.Select.TakeValue));

				if (base.BuildWhere(selectQuery))
				{
					StringBuilder.Append(" AND ");
					BuildSearchCondition(Precedence.LogicalConjunction, condition);
				}
			}
			else
				BuildSearchCondition(Precedence.Unknown, condition);
		}

		protected override void BuildFunction(SqlFunction func)
		{
			func = ConvertFunctionParameters(func);
			base.BuildFunction(func);
		}

		protected override void BuildDataType(SqlDataType type, bool createDbType)
		{
			switch (type.DataType)
			{
				case DataType.DateTime       : StringBuilder.Append("timestamp");                 break;
				case DataType.DateTime2      : StringBuilder.Append("timestamp");                 break;
				case DataType.DateTimeOffset : StringBuilder.Append("timestamp with time zone");  break;
				case DataType.UInt32         :
				case DataType.Int64          : StringBuilder.Append("Number(19)");                break;
				case DataType.SByte          :
				case DataType.Byte           : StringBuilder.Append("Number(3)");                 break;
				case DataType.Money          : StringBuilder.Append("Number(19,4)");              break;
				case DataType.SmallMoney     : StringBuilder.Append("Number(10,4)");              break;
				case DataType.NVarChar       :
					StringBuilder.Append("VarChar2");
					if (type.Length > 0)
						StringBuilder.Append('(').Append(type.Length).Append(')');
					break;
				case DataType.Boolean        : StringBuilder.Append("Char(1)");                   break;
				case DataType.NText          : StringBuilder.Append("NClob");                     break;
				case DataType.Text           : StringBuilder.Append("Clob");                      break;
				case DataType.Guid           : StringBuilder.Append("Raw(16)");                   break;
				case DataType.Binary         :
				case DataType.VarBinary      :
					if (type.Length == null || type.Length == 0)
						StringBuilder.Append("BLOB");
					else
						StringBuilder.Append("Raw(").Append(type.Length).Append(")");
					break;
				default: base.BuildDataType(type, createDbType);                                  break;
			}
		}

		protected override void BuildFromClause(SelectQuery selectQuery)
		{
			if (!selectQuery.IsUpdate)
				base.BuildFromClause(selectQuery);
		}

		protected override void BuildColumnExpression(SelectQuery selectQuery, ISqlExpression expr, string alias, ref bool addAlias)
		{
			var wrap = false;

			if (expr.SystemType == typeof(bool))
			{
				if (expr is SqlSearchCondition)
					wrap = true;
				else
				{
					var ex = expr as SqlExpression;
					wrap = ex != null && ex.Expr == "{0}" && ex.Parameters.Length == 1 && ex.Parameters[0] is SqlSearchCondition;
				}
			}

			if (wrap) StringBuilder.Append("CASE WHEN ");
			base.BuildColumnExpression(selectQuery, expr, alias, ref addAlias);
			if (wrap) StringBuilder.Append(" THEN 1 ELSE 0 END");
		}

		public override object Convert(object value, ConvertType convertType)
		{
			switch (convertType)
			{
				case ConvertType.NameToQueryParameter:
					return ":" + value;
			}

			return value;
		}

		protected override void BuildInsertOrUpdateQuery(SelectQuery selectQuery)
		{
			BuildInsertOrUpdateQueryAsMerge(selectQuery, "FROM SYS.DUAL");
		}

		public override string GetReserveSequenceValuesSql(int count, string sequenceName)
		{
			return "SELECT " + sequenceName + ".nextval ID from DUAL connect by level <= " + count;
		}

		protected override void BuildEmptyInsert(SelectQuery selectQuery)
		{
			StringBuilder.Append("VALUES ");

			foreach (var col in selectQuery.Insert.Into.Fields)
				StringBuilder.Append("(DEFAULT)");

			StringBuilder.AppendLine();
		}

		SqlField _identityField;

		public override int CommandCount(SqlStatement statement)
		{
			if (statement is SqlCreateTableStatement createTable)
			{
				_identityField = createTable.Table.Fields.Values.FirstOrDefault(f => f.IsIdentity);

				if (_identityField != null)
					return 3;
			}

			return base.CommandCount(statement);
		}

		protected override void BuildDropTableStatement(SqlDropTableStatement dropTable)
		{
			if (_identityField == null)
			{
				base.BuildDropTableStatement(dropTable);
			}
			else
			{
			var schemaPrefix = string.IsNullOrWhiteSpace(dropTable.Table.Owner)
				? string.Empty
				: dropTable.Table.Owner + ".";

				StringBuilder
					.Append("DROP TRIGGER ")
					.Append(schemaPrefix)
					.Append("TIDENTITY_")
					.Append(dropTable.Table.PhysicalName)
					.AppendLine();
			}
		}

		protected override void BuildCommand(int commandNumber)
		{
			string GetSchemaPrefix(SqlTable table)
			{
				return string.IsNullOrWhiteSpace(table.Owner)
					? string.Empty
					: table.Owner + ".";
			}

			switch (Statement)
			{
				case SqlDropTableStatement dropTable:
					{
						if (commandNumber == 1)
						{
							StringBuilder
								.Append("DROP SEQUENCE ")
								.Append(GetSchemaPrefix(dropTable.Table))
								.Append("SIDENTITY_")
								.Append(dropTable.Table.PhysicalName)
								.AppendLine();
						}
						else
							base.BuildDropTableStatement(dropTable);
						break;
					}
				case SqlCreateTableStatement createTable:
				{
					var schemaPrefix = GetSchemaPrefix(createTable.Table);

					if (commandNumber == 1)
					{
						StringBuilder
							.Append("CREATE SEQUENCE ")
							.Append(schemaPrefix)
							.Append("SIDENTITY_")
							.Append(createTable.Table.PhysicalName)
							.AppendLine();
					}
					else
					{
						StringBuilder
							.AppendFormat("CREATE OR REPLACE TRIGGER {0}TIDENTITY_{1}", schemaPrefix, createTable.Table.PhysicalName)
							.AppendLine()
							.AppendFormat("BEFORE INSERT ON ");

						BuildPhysicalTable(createTable.Table, null);

						StringBuilder
							.AppendLine(" FOR EACH ROW")
							.AppendLine  ()
							.AppendLine  ("BEGIN")
							.AppendFormat("\tSELECT {2}SIDENTITY_{1}.NEXTVAL INTO :NEW.{0} FROM dual;", _identityField.PhysicalName, createTable.Table.PhysicalName, schemaPrefix)
							.AppendLine  ()
							.AppendLine  ("END;");
					}
					break;
				}
			}
		}

		public override StringBuilder BuildTableName(StringBuilder sb, string database, string owner, string table)
		{
			if (owner != null && owner.Length == 0) owner = null;

			if (owner != null)
				sb.Append(owner).Append(".");

			return sb.Append(table);
		}

		protected override string GetProviderTypeName(IDbDataParameter parameter)
		{
			dynamic p = parameter;
			return p.OracleDbType.ToString();
		}
	}
}
