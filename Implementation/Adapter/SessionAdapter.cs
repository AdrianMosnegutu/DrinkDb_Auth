using System;
using System.Data;
using Microsoft.Data.SqlClient;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.Adapter
{
    public class SessionAdapter : ISessionAdapter
    {
        public Session CreateSession(Guid userId)
        {
            using SqlConnection databaseConnection = DrinkDbConnectionHelper.GetConnection();
            using SqlCommand createSessionCommand = new SqlCommand("create_session", databaseConnection);
            createSessionCommand.CommandType = CommandType.StoredProcedure;

            createSessionCommand.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
            SqlParameter sessionIdParameter = createSessionCommand.Parameters.Add("@sessionId", SqlDbType.UniqueIdentifier);
            sessionIdParameter.Direction = ParameterDirection.Output;

            createSessionCommand.ExecuteNonQuery();

            Guid sessionId = (Guid)sessionIdParameter.Value;
            return Session.createSessionWithIds(sessionId, userId);
        }

        public bool EndSession(Guid sessionId)
        {
            using SqlConnection databaseConnection = DrinkDbConnectionHelper.GetConnection();
            using SqlCommand endSessionCommand = new SqlCommand("end_session", databaseConnection);
            endSessionCommand.CommandType = CommandType.StoredProcedure;
            endSessionCommand.Parameters.Add("@sessionId", SqlDbType.UniqueIdentifier).Value = sessionId;

            SqlParameter returnValue = endSessionCommand.Parameters.Add("@RETURN_VALUE", SqlDbType.Int);
            returnValue.Direction = ParameterDirection.ReturnValue;

            endSessionCommand.ExecuteNonQuery();
            return (int)returnValue.Value > 0;
        }

        public Session GetSession(Guid sessionId)
        {
            using SqlConnection databaseConnection = DrinkDbConnectionHelper.GetConnection();
            using SqlCommand getSessionCommand = new SqlCommand("SELECT userId FROM Sessions WHERE sessionId = @sessionId", databaseConnection);
            getSessionCommand.Parameters.Add("@sessionId", SqlDbType.UniqueIdentifier).Value = sessionId;
            using SqlDataReader reader = getSessionCommand.ExecuteReader();
            if (reader.Read())
            {
                int firstColumn = 0;
                return Session.createSessionWithIds(sessionId, reader.GetGuid(firstColumn));
            }
            throw new Exception("Session not found.");
        }
    }
}