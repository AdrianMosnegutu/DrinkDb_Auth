using System;
using System.Data;
using System.Data.SqlClient;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.Adapter
{
    public class SessionAdapter
    {
        public Session CreateSession(Guid userId)
        {
            using var connection = DrinkDbConnectionHelper.CreateConnection();
            using var command = new SqlCommand("create_session", connection);
            command.CommandType = CommandType.StoredProcedure;
            
            command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
            var sessionIdParam = command.Parameters.Add("@sessionId", SqlDbType.UniqueIdentifier);
            sessionIdParam.Direction = ParameterDirection.Output;

            connection.Open();
            command.ExecuteNonQuery();
            
            var sessionId = (Guid)sessionIdParam.Value;
            return Session.createSessionWithIds(sessionId, userId);
        }

        public bool EndSession(Guid sessionId, Guid userId)
        {
            using var connection = DrinkDbConnectionHelper.CreateConnection();
            using var command = new SqlCommand("end_session", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@sessionId", SqlDbType.UniqueIdentifier).Value = sessionId;
            command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;

            connection.Open();
            var returnValue = command.Parameters.Add("@RETURN_VALUE", SqlDbType.Int);
            returnValue.Direction = ParameterDirection.ReturnValue;
            
            command.ExecuteNonQuery();
            return (int)returnValue.Value > 0;
        }

        public Session GetSession(Guid sessionId)
        {
            using var connection = DrinkDbConnectionHelper.CreateConnection();
            using var command = new SqlCommand(
                "SELECT userId FROM Sessions WHERE sessionId = @sessionId", 
                connection);
            command.Parameters.Add("@sessionId", SqlDbType.UniqueIdentifier).Value = sessionId;

            connection.Open();
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return Session.createSessionWithIds(sessionId, reader.GetGuid(0));
            }
            return null;
        }
    }
} 