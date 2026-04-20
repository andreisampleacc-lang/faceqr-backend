using MySql.Data.MySqlClient;

public class Database
{
    private string connectionString = "server=localhost;user=root;password=;database=faceqr_db;";

    public MySqlConnection GetConnection()
    {
        return new MySqlConnection(connectionString);
    }
}