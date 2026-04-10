using System.Data;

namespace ApixPress.App.Data.Context;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
