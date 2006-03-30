'*******************************************************************************************************
'  Tva.Data.Common.vb - Common Database Functions
'  Copyright � 2006 - TVA, all rights reserved - Gbtc
'
'  Build Environment: VB.NET, Visual Studio 2005
'  Primary Developer: James R Carroll, Operations Data Architecture [TVA]
'      Office: COO - TRNS/PWR ELEC SYS O, CHATTANOOGA, TN - MR 2W-C
'       Phone: 423/751-2827
'       Email: jrcarrol@tva.gov
'
'  Code Modification History:
'  -----------------------------------------------------------------------------------------------------
'  ??/??/???? - James R Carroll
'       Original version of source code generated
'  05/25/2004 - James R Carroll 
'       Added "with parameters" overloads to all basic query functions
'  06/21/2004 - James R Carroll
'       Added support for Oracle native .NET client since ESO systems can now work with this
'  12/10/2004 - Tim M Shults
'       Added several new WithParameters overloads that allow a programmer to send just the 
'       parameter values instead of creating a series of parameter objects and then sending 
'       them through.  Easy way to cut down on the amount of code.
'       This code is just for calls to Stored Procedures and will not work for in-line SQL
'  03/28/2006 - Pinal C Patel
'       2.0 version of source code migrated from 1.1 source (TVA.Shared.String)
'
'*******************************************************************************************************

Imports System.Data.OleDb
Imports System.Data.SqlClient
Imports System.Data.OracleClient
Imports System.ComponentModel
Imports System.Text
Imports System.Text.RegularExpressions
Imports Tva.DateTime.Common

Namespace Data

    Public NotInheritable Class Common

        Public Enum ConnectionType As Integer
            [OleDb]
            [SqlClient]
            [OracleClient]
        End Enum

        Private Sub New()

            ' This class contains only global functions and is not meant to be instantiated

        End Sub

        ''' <summary>
        ''' Performs Sql encoding on given string.
        ''' </summary>
        ''' <param name="sql">The string on which Sql encoding is to be performed.</param>
        ''' <returns>The Sql encoded string.</returns>
        ''' <remarks></remarks>
        Public Shared Function SqlEncode(ByVal sql As String) As String

            Return Replace(sql, "'", "''")

        End Function

#Region "ExecuteNonQuery Overloaded Functions"
        ' Executes given Sql update query for given connection string
        Public Shared Function ExecuteNonQuery(ByVal sql As String, ByVal connectString As String, ByVal connectionType As ConnectionType, ByVal timeout As Integer) As Integer

            Dim executionResult As Integer = -1
            Dim connection As IDbConnection = Nothing
            Dim command As IDbCommand = Nothing

            Select Case connectionType
                Case connectionType.SqlClient
                    connection = New SqlConnection(connectString)
                    command = New SqlCommand(sql, connection)
                Case connectionType.OracleClient
                    connection = New OracleConnection(connectString)
                    command = New OracleCommand(sql, connection)
                Case connectionType.OleDb
                    connection = New OleDbConnection(connectString)
                    command = New OleDbCommand(sql, connection)
            End Select

            connection.Open()
            command.CommandTimeout = timeout
            executionResult = command.ExecuteNonQuery()
            connection.Close()
            Return executionResult

        End Function

        ' Executes given Sql update query for given connection
        Public Shared Function ExecuteNonQuery(ByVal sql As String, ByVal connection As OleDbConnection, ByVal timeout As Integer) As Integer

            Return ExecuteNonQuery(sql, connection, timeout, DirectCast(Nothing, Object()))

        End Function

        <Obsolete("This function will be removed from future releases. Use the overload that takes a ParamArray of Object for the parameter 'parameters'.")> _
        Public Shared Function ExecuteNonQuery(ByVal sql As String, ByVal connection As OleDbConnection, ByVal timeout As Integer, ByVal ParamArray parameters As OleDbParameter()) As Integer

            Dim command As New OleDbCommand(sql, connection)
            command.CommandTimeout = timeout

            If Not parameters Is Nothing Then
                If parameters.Length > 0 Then
                    For Each param As OleDbParameter In parameters
                        command.Parameters.Add(param)
                    Next
                End If
            End If

            Return command.ExecuteNonQuery()

        End Function

        ' tmshults 12/10/2004 - This behaves exactly like the RetrieveDataSetWithParameters method except it uses the
        '                       cmd.ExecuteNonQuery to execute StoredProcedures that don't return data.
        Public Shared Function ExecuteNonQuery(ByVal sql As String, ByVal connection As OleDbConnection, ByVal timeout As Integer, ByVal ParamArray Parameters As Object()) As Integer

            Dim command As New OleDbCommand(sql, connection)
            command.CommandTimeout = timeout

            FillStoredProcParameters(command, ConnectionType.OleDb, Parameters)
            Return command.ExecuteNonQuery()

        End Function

        Public Shared Function ExecuteNonQuery(ByVal sql As String, ByVal connection As SqlConnection, ByVal timeout As Integer) As Integer

            Return ExecuteNonQuery(sql, connection, timeout, DirectCast(Nothing, Object()))

        End Function

        <Obsolete("This function will be removed from future releases. Use the overload that takes a ParamArray of Object for the parameter 'parameters'.")> _
        Public Shared Function ExecuteNonQuery(ByVal sql As String, ByVal connection As SqlConnection, ByVal timeout As Integer, ByVal ParamArray parameters As SqlParameter()) As Integer

            Dim command As New SqlCommand(sql, connection)
            command.CommandTimeout = timeout

            If Not parameters Is Nothing Then
                If parameters.Length > 0 Then
                    For Each param As SqlParameter In parameters
                        command.Parameters.Add(param)
                    Next
                End If
            End If

            Return command.ExecuteNonQuery()

        End Function

        ' tmshults 12/10/2004 - This behaves exactly like the RetrieveDataSetWithParameters method except it uses the
        '                       cmd.ExecuteNonQuery to execute StoredProcedures that don't return data.
        Public Shared Function ExecuteNonQuery(ByVal sql As String, ByVal connection As SqlConnection, ByVal timeout As Integer, ByVal ParamArray parameters As Object()) As Integer

            Dim command As New SqlCommand(sql, connection)
            command.CommandTimeout = timeout

            FillStoredProcParameters(command, ConnectionType.SqlClient, parameters)
            Return command.ExecuteNonQuery()

        End Function

        ' Executes given Sql update query for given connection
        Public Shared Function ExecuteNonQuery(ByVal sql As String, ByVal connection As OracleConnection) As Integer

            Return ExecuteNonQuery(sql, connection, DirectCast(Nothing, Object()))

        End Function

        <Obsolete("This function will be removed from future releases. Use the overload that takes a ParamArray of Object for the parameter 'parameters'.")> _
        Public Shared Function ExecuteNonQuery(ByVal sql As String, ByVal connection As OracleConnection, ByVal ParamArray parameters As OracleParameter()) As Integer

            Dim command As New OracleCommand(sql, connection)

            If Not parameters Is Nothing Then
                If parameters.Length > 0 Then
                    For Each param As OracleParameter In parameters
                        command.Parameters.Add(param)
                    Next
                End If
            End If

            Return command.ExecuteNonQuery()

        End Function

        ' tmshults 12/10/2004 - This behaves exactly like the RetrieveDataSetWithParameters method except it uses the
        '                       cmd.ExecuteNonQuery to execute StoredProcedures that don't return data.
        Public Shared Function ExecuteNonQuery(ByVal sql As String, ByVal connection As OracleConnection, ByVal ParamArray parameters As Object()) As Integer

            Dim command As New OracleCommand(sql, connection)

            FillStoredProcParameters(command, ConnectionType.OracleClient, parameters)
            Return command.ExecuteNonQuery()

        End Function
#End Region

#Region "ExecuteReader Overloaded Functions"
        ' Executes given Sql data query for given connection
        Public Shared Function ExecuteReader(ByVal sql As String, ByVal connection As OleDbConnection, ByVal behavior As CommandBehavior, ByVal timeout As Integer) As OleDbDataReader

            Return ExecuteReader(sql, connection, behavior, timeout, DirectCast(Nothing, Object()))

        End Function

        <Obsolete("This function will be removed from future releases. Use the overload that takes a ParamArray of Object for the parameter 'parameters'.")> _
        Public Shared Function ExecuteReader(ByVal sql As String, ByVal connection As OleDbConnection, ByVal behavior As CommandBehavior, ByVal timeout As Integer, ByVal ParamArray parameters As OleDbParameter()) As OleDbDataReader

            Dim command As New OleDbCommand(sql, connection)
            command.CommandTimeout = timeout

            If Not parameters Is Nothing Then
                If parameters.Length > 0 Then
                    For Each param As OleDbParameter In parameters
                        command.Parameters.Add(param)
                    Next
                End If
            End If

            Return command.ExecuteReader(behavior)

        End Function

        ' tmshults 12/10/2004 - This behaves exactly like the RetrieveDataSetWithParameters method except it uses the
        '                       cmd.ExecuteReader as the data that is returned.
        Public Shared Function ExecuteReader(ByVal sql As String, ByVal connection As OleDbConnection, ByVal behavior As CommandBehavior, ByVal timeout As Integer, ByVal ParamArray parameters As Object()) As OleDbDataReader

            Dim command As New OleDbCommand(sql, connection)
            command.CommandTimeout = timeout

            FillStoredProcParameters(command, ConnectionType.OleDb, Parameters)
            Return command.ExecuteReader(behavior)

        End Function

        ' Executes given Sql data query for given connection
        Public Shared Function ExecuteReader(ByVal sql As String, ByVal connection As SqlConnection, ByVal behavior As CommandBehavior, ByVal timeout As Integer) As SqlDataReader

            Return ExecuteReader(sql, connection, behavior, timeout, DirectCast(Nothing, Object()))

        End Function

        <Obsolete("This function will be removed from future releases. Use the overload that takes a ParamArray of Object for the parameter 'parameters'.")> _
        Public Shared Function ExecuteReader(ByVal sql As String, ByVal connection As SqlConnection, ByVal behavior As CommandBehavior, ByVal timeout As Integer, ByVal ParamArray parameters As SqlParameter()) As SqlDataReader

            Dim command As New SqlCommand(sql, connection)
            command.CommandTimeout = timeout

            If Not parameters Is Nothing Then
                If parameters.Length > 0 Then
                    For Each param As SqlParameter In parameters
                        command.Parameters.Add(param)
                    Next
                End If
            End If

            Return command.ExecuteReader(behavior)

        End Function

        ' tmshults 12/10/2004 - This behaves exactly like the RetrieveDataSetWithParameters method except it uses the
        '                       cmd.ExecuteReader as the data that is returned.
        Public Shared Function ExecuteReader(ByVal sql As String, ByVal connection As SqlConnection, ByVal behavior As CommandBehavior, ByVal timeout As Integer, ByVal ParamArray parameters As Object()) As SqlDataReader

            Dim command As New SqlCommand(sql, connection)
            command.CommandTimeout = timeout

            FillStoredProcParameters(command, ConnectionType.SqlClient, Parameters)
            Return command.ExecuteReader(behavior)

        End Function

        ' Executes given Sql data query for given connection
        Public Shared Function ExecuteReader(ByVal sql As String, ByVal connection As OracleConnection, ByVal behavior As CommandBehavior) As OracleDataReader

            Return ExecuteReader(sql, connection, behavior, DirectCast(Nothing, Object()))

        End Function

        <Obsolete("This function will be removed from future releases. Use the overload that takes a ParamArray of Object for the parameter 'parameters'.")> _
        Public Shared Function ExecuteReader(ByVal sql As String, ByVal connection As OracleConnection, ByVal behavior As CommandBehavior, ByVal ParamArray parameters As OracleParameter()) As OracleDataReader

            Dim command As New OracleCommand(sql, Connection)

            If Not parameters Is Nothing Then
                If parameters.Length > 0 Then
                    For Each param As OracleParameter In parameters
                        command.Parameters.Add(param)
                    Next
                End If
            End If

            Return command.ExecuteReader(behavior)

        End Function

        ' tmshults 12/10/2004 - This behaves exactly like the RetrieveDataSetWithParameters method except it uses the
        '                       cmd.ExecuteReader as the data that is returned.
        Public Shared Function ExecuteReader(ByVal sql As String, ByVal connection As OracleConnection, ByVal behavior As CommandBehavior, ByVal ParamArray parameters As Object()) As OracleDataReader

            Dim command As New OracleCommand(sql, connection)

            FillStoredProcParameters(command, ConnectionType.OracleClient, Parameters)
            Return command.ExecuteReader(behavior)

        End Function
#End Region

#Region "ExecuteScalar Overloaded Functions"
        ' Executes given Sql scalar query for given connection
        Public Shared Function ExecuteScalar(ByVal sql As String, ByVal connection As OleDbConnection, ByVal timeout As Integer) As Object

            Return ExecuteScalar(sql, connection, timeout, DirectCast(Nothing, Object()))

        End Function

        <Obsolete("This function will be removed from future releases. Use the overload that takes a ParamArray of Object for the parameter 'parameters'.")> _
        Public Shared Function ExecuteScalar(ByVal sql As String, ByVal connection As OleDbConnection, ByVal timeout As Integer, ByVal ParamArray parameters As OleDbParameter()) As Object

            Dim command As New OleDbCommand(sql, connection)
            command.CommandTimeout = timeout

            If Not parameters Is Nothing Then
                If Parameters.Length > 0 Then
                    For Each param As OleDbParameter In parameters
                        command.Parameters.Add(param)
                    Next
                End If
            End If

            Return command.ExecuteScalar()

        End Function

        ' tmshults 12/10/2004 - This behaves exactly like the RetrieveDataSetWithParameters method except it uses the
        '                       cmd.ExecuteScalar as the data that is returned.
        Public Shared Function ExecuteScalar(ByVal sql As String, ByVal connection As OleDbConnection, ByVal timeout As Integer, ByVal ParamArray parameters As Object()) As Object

            Dim command As New OleDbCommand(sql, connection)
            command.CommandTimeout = timeout

            FillStoredProcParameters(command, ConnectionType.OleDb, parameters)
            Return command.ExecuteScalar()

        End Function

        ' Executes given Sql scalar query for given connection
        Public Shared Function ExecuteScalar(ByVal sql As String, ByVal connection As SqlConnection, ByVal timeout As Integer) As Object

            Return ExecuteScalar(sql, connection, timeout, DirectCast(Nothing, Object()))

        End Function

        <Obsolete("This function will be removed from future releases. Use the overload that takes a ParamArray of Object for the parameter 'parameters'.")> _
        Public Shared Function ExecuteScalar(ByVal sql As String, ByVal connection As SqlConnection, ByVal timeout As Integer, ByVal ParamArray parameters As SqlParameter()) As Object

            Dim command As New SqlCommand(sql, connection)
            command.CommandTimeout = timeout

            If Not parameters Is Nothing Then
                If parameters.Length > 0 Then
                    For Each param As SqlParameter In parameters
                        command.Parameters.Add(param)
                    Next
                End If
            End If

            Return command.ExecuteScalar()

        End Function

        ' tmshults 12/10/2004 - This behaves exactly like the RetrieveDataSetWithParameters method except it uses the
        '                       cmd.ExecuteScalar as the data that is returned.
        Public Shared Function ExecuteScalar(ByVal sql As String, ByVal connection As SqlConnection, ByVal timeout As Integer, ByVal ParamArray parameters As Object()) As Object

            Dim command As New SqlCommand(sql, connection)
            command.CommandTimeout = timeout

            FillStoredProcParameters(command, ConnectionType.SqlClient, parameters)
            Return command.ExecuteScalar()

        End Function

        ' Executes given Sql scalar query for given connection
        Public Shared Function ExecuteScalar(ByVal sql As String, ByVal connection As OracleConnection) As Object

            Return ExecuteScalar(sql, connection, DirectCast(Nothing, Object()))

        End Function

        <Obsolete("This function will be removed from future releases. Use the overload that takes a ParamArray of Object for the parameter 'parameters'.")> _
        Public Shared Function ExecuteScalar(ByVal sql As String, ByVal connection As OracleConnection, ByVal ParamArray parameters As OracleParameter()) As Object

            Dim command As New OracleCommand(sql, Connection)

            If Not parameters Is Nothing Then
                If parameters.Length > 0 Then
                    For Each param As OracleParameter In parameters
                        command.Parameters.Add(param)
                    Next
                End If
            End If

            Return command.ExecuteScalar()

        End Function

        ' tmshults 12/10/2004 - This behaves exactly like the RetrieveDataSetWithParameters method except it uses the
        '                       cmd.ExecuteScalar as the data that is returned.
        Public Shared Function ExecuteScalar(ByVal sql As String, ByVal connection As OracleConnection, ByVal ParamArray parameters As Object()) As Object

            Dim command As New OracleCommand(sql, connection)

            FillStoredProcParameters(command, ConnectionType.OracleClient, parameters)
            Return command.ExecuteScalar()

        End Function
#End Region

#Region "RetrieveRow Overloaded Functions"
        ' Return a single row of data given a Sql statement and connection
        Public Shared Function RetrieveRow(ByVal sql As String, ByVal connection As OleDbConnection, ByVal timeout As Integer) As DataRow

            Return RetrieveRow(sql, connection, timeout, DirectCast(Nothing, Object()))

        End Function

        <Obsolete("This function will be removed from future releases. Use the overload that takes a ParamArray of Object for the parameter 'parameters'.")> _
        Public Shared Function RetrieveRow(ByVal sql As String, ByVal connection As OleDbConnection, ByVal timeout As Integer, ByVal ParamArray parameters As OleDbParameter()) As DataRow

            With RetrieveData(sql, connection, 0, 1, timeout, parameters)
                If .Rows.Count = 0 Then .Rows.Add(.NewRow())
                Return .Rows(0)
            End With

        End Function

        ' tmshults 12/10/2004 - This behaves exactly like the RetrieveDataSetWithParameters method except it returns  
        '                       the first row returned from the base DataTable that is linked to the underlying DataSet
        Public Shared Function RetrieveRow(ByVal sql As String, ByVal connection As OleDbConnection, ByVal timeout As Integer, ByVal ParamArray parameters As Object()) As DataRow

            With RetrieveData(sql, connection, 0, 1, timeout, parameters)
                If .Rows.Count = 0 Then .Rows.Add(.NewRow())
                Return .Rows(0)
            End With

        End Function

        ' Return a single row of data given a Sql statement and connection
        Public Shared Function RetrieveRow(ByVal sql As String, ByVal connection As SqlConnection, ByVal timeout As Integer) As DataRow

            Return RetrieveRow(sql, connection, timeout, DirectCast(Nothing, Object()))

        End Function

        <Obsolete("This function will be removed from future releases. Use the overload that takes a ParamArray of Object for the parameter 'parameters'.")> _
        Public Shared Function RetrieveRow(ByVal sql As String, ByVal connection As SqlConnection, ByVal timeout As Integer, ByVal ParamArray parameters As SqlParameter()) As DataRow

            With RetrieveData(sql, connection, 0, 1, timeout, parameters)
                If .Rows.Count = 0 Then .Rows.Add(.NewRow())
                Return .Rows(0)
            End With

        End Function

        ' tmshults 12/10/2004 - This behaves exactly like the RetrieveDataSetWithParameters method except it returns  
        '                       the first row returned from the base DataTable that is linked to the underlying DataSet
        Public Shared Function RetrieveRow(ByVal sql As String, ByVal connection As SqlConnection, ByVal timeout As Integer, ByVal ParamArray parameters As Object()) As DataRow

            With RetrieveData(sql, connection, 0, 1, timeout, parameters)
                If .Rows.Count = 0 Then .Rows.Add(.NewRow())
                Return .Rows(0)
            End With

        End Function

        ' Return a single row of data given a Sql statement and connection
        Public Shared Function RetrieveRow(ByVal sql As String, ByVal connection As OracleConnection) As DataRow

            Return RetrieveRow(sql, connection, DirectCast(Nothing, Object()))

        End Function

        <Obsolete("This function will be removed from future releases. Use the overload that takes a ParamArray of Object for the parameter 'parameters'.")> _
        Public Shared Function RetrieveRow(ByVal sql As String, ByVal connection As OracleConnection, ByVal ParamArray parameters As OracleParameter()) As DataRow

            With RetrieveData(sql, connection, 0, 1, parameters)
                If .Rows.Count = 0 Then .Rows.Add(.NewRow())
                Return .Rows(0)
            End With

        End Function

        ' tmshults 12/10/2004 - This behaves exactly like the RetrieveDataSetWithParameters method except it returns  
        '                       the first row returned from the base DataTable that is linked to the underlying DataSet
        Public Shared Function RetrieveRow(ByVal sql As String, ByVal connection As OracleConnection, ByVal ParamArray parameters As Object()) As DataRow

            With RetrieveData(sql, connection, 0, 1, parameters)
                If .Rows.Count = 0 Then .Rows.Add(.NewRow())
                Return .Rows(0)
            End With

        End Function
#End Region

#Region "RetrieveData Overloaded Functions"
        ' Return a data table given a Sql statement and connection
        Public Shared Function RetrieveData(ByVal sql As String, ByVal connection As OleDbConnection, ByVal startRow As Integer, ByVal maxRows As Integer, ByVal timeout As Integer) As DataTable

            Return RetrieveData(sql, connection, startRow, maxRows, timeout, DirectCast(Nothing, Object()))

        End Function

        <Obsolete("This function will be removed from future releases. Use the overload that takes a ParamArray of Object for the parameter 'parameters'.")> _
        Public Shared Function RetrieveData(ByVal sql As String, ByVal connection As OleDbConnection, ByVal startRow As Integer, ByVal maxRows As Integer, ByVal timeout As Integer, ByVal ParamArray parameters As OleDbParameter()) As DataTable

            Return RetrieveDataSet(sql, connection, startRow, maxRows, timeout, parameters).Tables(0)

        End Function

        ' tmshults 12/10/2004 - This behaves exactly like the RetrieveDataSetWithParameters method except it returns the 
        '                       base DataTable that is linked to the underlying DataSet
        Public Shared Function RetrieveData(ByVal sql As String, ByVal connection As OleDbConnection, ByVal startRow As Integer, ByVal maxRows As Integer, ByVal timeout As Integer, ByVal ParamArray parameters As Object()) As DataTable

            Return RetrieveDataSet(sql, connection, startRow, maxRows, timeout, parameters).Tables(0)

        End Function

        ' Return a data table given a Sql statement and connection
        Public Shared Function RetrieveData(ByVal sql As String, ByVal connection As SqlConnection, ByVal startRow As Integer, ByVal maxRows As Integer, ByVal timeout As Integer) As DataTable

            Return RetrieveData(sql, connection, startRow, maxRows, timeout, DirectCast(Nothing, Object()))

        End Function

        <Obsolete("This function will be removed from future releases. Use the overload that takes a ParamArray of Object for the parameter 'parameters'.")> _
        Public Shared Function RetrieveData(ByVal sql As String, ByVal connection As SqlConnection, ByVal startRow As Integer, ByVal maxRows As Integer, ByVal timeout As Integer, ByVal ParamArray parameters As SqlParameter()) As DataTable

            Return RetrieveDataSetWithParameters(sql, connection, startRow, maxRows, timeout, parameters).Tables(0)

        End Function

        ' tmshults 12/10/2004 - This behaves exactly like the RetrieveDataSetWithParameters method except it returns the 
        '                       base DataTable that is linked to the underlying DataSet
        Public Shared Function RetrieveData(ByVal sql As String, ByVal connection As SqlConnection, ByVal startRow As Integer, ByVal maxRows As Integer, ByVal timeout As Integer, ByVal ParamArray parameters As Object()) As DataTable

            Return RetrieveDataSetWithParameters(sql, connection, startRow, maxRows, timeout, parameters).Tables(0)

        End Function

        ' Return a data table given a Sql statement and connection
        Public Shared Function RetrieveData(ByVal sql As String, ByVal connection As OracleConnection, ByVal startRow As Integer, ByVal maxRows As Integer) As DataTable

            Return RetrieveData(sql, connection, startRow, maxRows, DirectCast(Nothing, Object()))

        End Function

        <Obsolete("This function will be removed from future releases. Use the overload that takes a ParamArray of Object for the parameter 'parameters'.")> _
        Public Shared Function RetrieveData(ByVal sql As String, ByVal connection As OracleConnection, ByVal startRow As Integer, ByVal maxRows As Integer, ByVal ParamArray parameters As OracleParameter()) As DataTable

            Return RetrieveDataSetWithParameters(sql, connection, startRow, maxRows, parameters).Tables(0)

        End Function

        ' tmshults 12/10/2004 - This behaves exactly like the RetrieveDataSetWithParameters method except it returns the 
        '                       base DataTable that is linked to the underlying DataSet
        Public Shared Function RetrieveData(ByVal sql As String, ByVal connection As OracleConnection, ByVal startRow As Integer, ByVal maxRows As Integer, ByVal ParamArray parameters As Object()) As DataTable

            Return RetrieveDataSetWithParameters(sql, connection, startRow, maxRows, parameters).Tables(0)

        End Function
#End Region

#Region "RetrieveDataSet Overloaded Functions"
        <Obsolete("This function will be removed from future releases. Use the overload that takes a ParamArray of Object for the parameter 'parameters'.")> _
        Public Shared Function RetrieveDataSet(ByVal sql As String, ByVal connection As OleDbConnection, ByVal startRow As Integer, ByVal maxRows As Integer, ByVal timeout As Integer, ByVal ParamArray parameters As OleDbParameter()) As DataSet

            Dim command As New OleDbCommand(sql, connection)
            command.CommandTimeout = timeout

            If Not parameters Is Nothing Then
                If parameters.Length > 0 Then
                    For Each param As OleDbParameter In parameters
                        command.Parameters.Add(param)
                    Next
                End If
            End If

            Dim dataAdapter As New OleDbDataAdapter(command)
            Dim data As New DataSet("Temp")
            dataAdapter.Fill(data, startRow, maxRows, "Table1")

            Return data

        End Function

        ' tmshults 12/10/2004 - Added this method as an easy way to populate a DataSet with a StoredProc call
        '                       This takes the given values and then populates the appropriate Parameters for
        '                       the StoredProc.
        Public Shared Function RetrieveDataSet(ByVal sql As String, ByVal connection As OleDbConnection, ByVal startRow As Integer, ByVal maxRows As Integer, ByVal timeout As Integer, ByVal ParamArray parameters As Object()) As DataSet

            Dim command As New OleDbCommand(sql, connection)

            FillStoredProcParameters(command, ConnectionType.OleDb, parameters)

            Dim dataAdapter As New OleDbDataAdapter(command)
            Dim data As New DataSet("Temp")
            dataAdapter.Fill(data, startRow, maxRows, "Table1")

            Return data

        End Function

        Public Shared Function RetrieveDataSetWithParameters(ByVal Sql As String, ByVal Connection As SqlConnection, ByVal StartRow As Integer, ByVal MaxRows As Integer, ByVal Timeout As Integer, ByVal ParamArray Parameters As SqlParameter()) As DataSet

            Dim cmd As New SqlCommand(Sql, Connection)

            cmd.CommandTimeout = Timeout

            If Not Parameters Is Nothing Then
                If Parameters.Length > 0 Then
                    For Each Param As SqlParameter In Parameters
                        cmd.Parameters.Add(Param)
                    Next
                End If
            End If

            Dim da As New SqlDataAdapter(cmd)
            Dim ds As New DataSet("Temp")

            da.Fill(ds, StartRow, MaxRows, "Table1")

            Return ds

        End Function

        ' tmshults 12/10/2004 - Added this method as an easy way to populate a DataSet with a StoredProc call
        '                       This takes the given values and then populates the appropriate Parameters for
        '                       the StoredProc.
        Public Shared Function RetrieveDataSetWithParameters(ByVal StoredProcName As String, ByVal Connection As SqlConnection, ByVal StartRow As Integer, ByVal MaxRows As Integer, ByVal Timeout As Integer, ByVal ParamArray Parameters As Object()) As DataSet

            Dim cmd As New SqlCommand(StoredProcName, Connection)

            cmd.CommandTimeout = Timeout

            FillStoredProcParameters(cmd, ConnectionType.SqlClient, Parameters)

            Dim da As New SqlDataAdapter(cmd)
            Dim ds As New DataSet("Temp")

            da.Fill(ds, StartRow, MaxRows, "Table1")

            Return ds

        End Function

        Public Shared Function RetrieveDataSet(ByVal Sql As String, ByVal Connection As OracleConnection, Optional ByVal StartRow As Integer = 0, Optional ByVal MaxRows As Integer = Integer.MaxValue) As DataSet

            Return RetrieveDataSetWithParameters(Sql, Connection, StartRow, MaxRows, DirectCast(Nothing, OracleParameter()))

        End Function

        Public Shared Function RetrieveDataSetWithParameters(ByVal Sql As String, ByVal Connection As OracleConnection, ByVal StartRow As Integer, ByVal MaxRows As Integer, ByVal ParamArray Parameters As OracleParameter()) As DataSet

            Dim cmd As New OracleCommand(Sql, Connection)

            If Not Parameters Is Nothing Then
                If Parameters.Length > 0 Then
                    For Each Param As OracleParameter In Parameters
                        cmd.Parameters.Add(Param)
                    Next
                End If
            End If

            Dim da As New OracleDataAdapter(cmd)
            Dim ds As New DataSet("Temp")

            da.Fill(ds, StartRow, MaxRows, "Table1")

            Return ds

        End Function

        ' tmshults 12/10/2004 - Added this method as an easy way to populate a DataSet with a StoredProc call
        '                       This takes the given values and then populates the appropriate Parameters for
        '                       the StoredProc.
        Public Shared Function RetrieveDataSetWithParameters(ByVal StoredProcName As String, ByVal Connection As OracleConnection, ByVal StartRow As Integer, ByVal MaxRows As Integer, ByVal ParamArray Parameters As Object()) As DataSet

            Dim cmd As New OracleCommand(StoredProcName, Connection)

            FillStoredProcParameters(cmd, ConnectionType.OracleClient, Parameters)

            Dim da As New OracleDataAdapter(cmd)
            Dim ds As New DataSet("Temp")

            da.Fill(ds, StartRow, MaxRows, "Table1")

            Return ds

        End Function
#End Region

        ' tmshults 12/10/2004 - This is the private method that takes the passed Command Object queries what the 
        '                       parameters are for the given StoredProcedure and then populates the values of the 
        '                       command used to populate DataSets, Datatables, DataReaders or just used simply to 
        '                       execute the required code with no need to return any data.
        Private Shared Sub FillStoredProcParameters(ByRef command As IDbCommand, ByVal connectionType As ConnectionType, ByVal parameters() As Object)

            If parameters IsNot Nothing Then
                ' This is required for the SqlCommandBuilder to call Derive Parameters
                command.CommandType = CommandType.StoredProcedure

                ' Makes quick query to db to find the parameters for the StoredProc 
                ' and then creates them for the command
                Select Case connectionType
                    Case connectionType.SqlClient
                        SqlClient.SqlCommandBuilder.DeriveParameters(command)
                    Case connectionType.OracleClient
                        OracleClient.OracleCommandBuilder.DeriveParameters(command)
                    Case connectionType.OleDb
                        OleDb.OleDbCommandBuilder.DeriveParameters(command)
                End Select

                ' Remove the ReturnValue Parameter
                command.Parameters.RemoveAt(0)

                ' Check to see if the Parameters found match the Values provide
                If command.Parameters.Count() <> parameters.Length() Then
                    ' If there are more values provide than parameters throw an error
                    If parameters.Length > command.Parameters.Count Then _
                        Throw New ArgumentException("You have supplied more Values than Parameters listed for the Stored Procedure")

                    ' Otherwise assume that the missing values are for Parameters that have default values
                    ' and the code is willing to use the default.  To do this fill the extended ParamValue as Nothing/Null
                    ReDim Preserve parameters(command.Parameters.Count - 1) ' Make the Values array match the Parameters of the Stored Proc
                End If

                ' Assign the values to the the Parameters.
                For i As Integer = 0 To command.Parameters.Count() - 1
                    command.Parameters(i).Value = parameters(i)
                Next
            End If

        End Sub

        'Pinal Patel 04/06/05 - Updates the underlying data source of the specified data table.
        Public Shared Function UpdateData(ByVal SourceData As DataTable, ByVal SourceSql As String, ByVal Connection As SqlConnection) As Integer

            Dim da As SqlDataAdapter = New SqlDataAdapter(SourceSql, Connection)
            Dim cb As SqlCommandBuilder = New SqlCommandBuilder(da)
            Return da.Update(SourceData)

        End Function

        'Pinal Patel 04/06/05 - Updates the underlying data source of the specified data table.
        Public Shared Function UpdateData(ByVal SourceData As DataTable, ByVal SourceSql As String, ByVal Connection As OleDbConnection) As Integer

            Dim da As OleDbDataAdapter = New OleDbDataAdapter(SourceSql, Connection)
            Dim cb As OleDbCommandBuilder = New OleDbCommandBuilder(da)
            Return da.Update(SourceData)

        End Function

        'Pinal Patel 04/06/05 - Updates the underlying data source of the specified data table.
        Public Shared Function UpdateData(ByVal SourceData As DataTable, ByVal SourceSql As String, ByVal Connection As OracleConnection) As Integer

            Dim da As OracleDataAdapter = New OracleDataAdapter(SourceSql, Connection)
            Dim cb As OracleCommandBuilder = New OracleCommandBuilder(da)
            Return da.Update(SourceData)

        End Function

        'Pinal Patel 05/27/05 - Converts delimited data to data table.
        Public Shared Function DelimitedDataToDataTable(ByVal DelimitedData As String, Optional ByVal Delimiter As String = ",", Optional ByVal Header As Boolean = True) As DataTable

            Dim dtResult As DataTable = New DataTable
            Dim strPattern As String = Regex.Escape(Delimiter) & "(?=(?:[^""]*""[^""]*"")*(?![^""]*""))" 'Regex pattern that will be used to split the delimited data.


            DelimitedData = DelimitedData.Trim(New Char() {" "c, vbCr, vbLf}).Replace(vbLf, "") 'Remove any leading and trailing whitespaces, carriage returns or line feeds.
            Dim strLines() As String = DelimitedData.Split(vbCr)  'Split delimited data into lines.


            Dim intCursor As Integer = 0
            'Assume that the first line has header information.
            Dim strHeaders() As String = Regex.Split(strLines(intCursor), strPattern)
            'Create columns.
            If Header Then
                'Use the first row as header row.
                For i As Integer = 0 To strHeaders.Length() - 1
                    dtResult.Columns.Add(New DataColumn(strHeaders(i).Trim(New Char() {""""c}))) 'Remove any leading and trailing quotes from the column name.
                Next
                intCursor += 1
            Else
                For i As Integer = 0 To strHeaders.Length() - 1
                    dtResult.Columns.Add(New DataColumn)
                Next
            End If


            'Populate the data table with csv data.
            For intCursor = intCursor To strLines.Length() - 1
                Dim drResult As DataRow = dtResult.NewRow() 'Create new row.

                'Populate the new row.
                Dim strFields() As String = Regex.Split(strLines(intCursor), strPattern)
                For i As Integer = 0 To strFields.Length() - 1
                    drResult(i) = strFields(i).Trim(New Char() {""""c})    'Remove any leading and trailing quotes from the data.
                Next

                dtResult.Rows.Add(drResult) 'Add the new row.
            Next


            'Return the data table.
            Return dtResult

        End Function

        'Pinal Patel 05/27/05 - Converts a data table to delimited data.
        Public Shared Function DataTableToDelimitedData(ByVal Table As DataTable, Optional ByVal Delimiter As String = ",", Optional ByVal Quoted As Boolean = True, Optional ByVal Header As Boolean = True) As String

            With New StringBuilder
                'Use the column names as the headers if headers are requested.
                If Header Then
                    For i As Integer = 0 To Table.Columns().Count() - 1
                        .Append(IIf(Quoted, """", "") & Table.Columns(i).ColumnName() & IIf(Quoted, """", ""))

                        If i < Table.Columns().Count() - 1 Then
                            .Append(Delimiter)
                        End If
                    Next
                    .Append(vbCrLf)
                End If

                For i As Integer = 0 To Table.Rows().Count() - 1
                    'Convert data table's data to delimited data.
                    For j As Integer = 0 To Table.Columns().Count() - 1
                        .Append(IIf(Quoted, """", "") & Table.Rows(i)(j) & IIf(Quoted, """", ""))

                        If j < Table.Columns().Count() - 1 Then
                            .Append(Delimiter)
                        End If
                    Next
                    .Append(vbCrLf)
                Next

                'Return the delimited data.
                Return .ToString()
            End With

        End Function

    End Class

End Namespace