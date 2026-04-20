using MySql.Data.MySqlClient;

public class Database
{
    private string connectionString;

    public Database()
    {
        string host     = Environment.GetEnvironmentVariable("MYSQLHOST")     ?? "localhost";
        string port     = Environment.GetEnvironmentVariable("MYSQLPORT")     ?? "3306";
        string user     = Environment.GetEnvironmentVariable("MYSQLUSER")     ?? "root";
        string password = Environment.GetEnvironmentVariable("MYSQLPASSWORD") ?? "";
        string database = Environment.GetEnvironmentVariable("MYSQLDATABASE") ?? "faceqr_db";

        connectionString = $"server={host};port={port};user={user};password={password};database={database};";
    }

    public MySqlConnection GetConnection()
    {
        return new MySqlConnection(connectionString);
    }
}