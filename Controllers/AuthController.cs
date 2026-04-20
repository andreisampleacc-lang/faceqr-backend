using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using QRCoder;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        Database db = new Database();

        // ─── HEALTH CHECK ────────────────────────────────────────
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new { status = "Backend working ✅" });
        }

        // ─── GET ALL USERS (admin panel) ─────────────────────────
        [HttpGet("users")]
        public IActionResult GetUsers()
        {
            var users = new List<object>();
            using (var conn = db.GetConnection())
            {
                conn.Open();
                var cmd = new MySqlCommand(
                    "SELECT id, username, qr_code, is_approved, face_data, created_at FROM users", conn);
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var approvedRaw = reader["is_approved"].ToString().ToLower();
                    bool isApproved = approvedRaw == "1" || approvedRaw == "true";
                    users.Add(new {
                        id        = reader["id"].ToString(),
                        username  = reader["username"].ToString(),
                        qrCode    = reader["qr_code"].ToString(),
                        approved  = isApproved,
                        hasFace   = !string.IsNullOrEmpty(reader["face_data"].ToString()),
                        createdAt = reader["created_at"].ToString()
                    });
                }
            }
            return Ok(users);
        }

        // ─── REGISTER ────────────────────────────────────────────
        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { error = "Username and password are required" });

            using (var conn = db.GetConnection())
            {
                conn.Open();

                var check = new MySqlCommand(
                    "SELECT COUNT(*) FROM users WHERE username=@u", conn);
                check.Parameters.AddWithValue("@u", req.Username);
                if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                    return BadRequest(new { error = "Username already exists" });

                string qrCode = Guid.NewGuid().ToString();

                var cmd = new MySqlCommand(
                    "INSERT INTO users (username, password, face_data, qr_code, is_approved) VALUES (@u, @p, @f, @q, 0)",
                    conn);
                cmd.Parameters.AddWithValue("@u", req.Username);
                cmd.Parameters.AddWithValue("@p", req.Password);
                cmd.Parameters.AddWithValue("@f", req.FaceData ?? "");
                cmd.Parameters.AddWithValue("@q", qrCode);
                cmd.ExecuteNonQuery();

                return Ok(new {
                    message  = "Registered successfully",
                    qrCode,
                    qrImage  = GenerateQR(qrCode)
                });
            }
        }

        // ─── SAVE FACE DATA ──────────────────────────────────────
        [HttpPost("save-face")]
        public IActionResult SaveFace([FromBody] SaveFaceRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.FaceData))
                return BadRequest(new { error = "Username and face data are required" });

            using (var conn = db.GetConnection())
            {
                conn.Open();
                var cmd = new MySqlCommand(
                    "UPDATE users SET face_data=@f WHERE username=@u", conn);
                cmd.Parameters.AddWithValue("@f", req.FaceData);
                cmd.Parameters.AddWithValue("@u", req.Username);
                int affected = cmd.ExecuteNonQuery();

                if (affected == 0)
                    return NotFound(new { error = "User not found" });
            }
            return Ok(new { message = "Face saved ✅" });
        }

        // ─── LOGIN STEP 1 — PASSWORD ──────────────────────────────
        [HttpPost("login/password")]
        public IActionResult LoginPassword([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { error = "Username and password are required" });

            using (var conn = db.GetConnection())
            {
                conn.Open();
                var cmd = new MySqlCommand(
                    "SELECT id, username, qr_code, is_approved FROM users WHERE username=@u AND password=@p",
                    conn);
                cmd.Parameters.AddWithValue("@u", req.Username);
                cmd.Parameters.AddWithValue("@p", req.Password);
                var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    var approvedRaw = reader["is_approved"].ToString().ToLower();
                    bool isApproved = approvedRaw == "1" || approvedRaw == "true";

                    if (!isApproved)
                        return Unauthorized(new { error = "Account not yet approved by admin" });

                    return Ok(new {
                        status   = "PASSWORD_OK",
                        username = reader["username"].ToString(),
                        qrCode   = reader["qr_code"].ToString()
                    });
                }
            }
            return Unauthorized(new { error = "Wrong username or password" });
        }

        // ─── LOGIN STEP 2 — QR ───────────────────────────────────
        [HttpPost("login/qr")]
        public IActionResult LoginQR([FromBody] string qr)
        {
            if (string.IsNullOrWhiteSpace(qr))
                return BadRequest(new { error = "QR code is required" });

            using (var conn = db.GetConnection())
            {
                conn.Open();
                var cmd = new MySqlCommand(
                    "SELECT username, face_data FROM users WHERE qr_code=@q AND (is_approved=1 OR is_approved=true)",
                    conn);
                cmd.Parameters.AddWithValue("@q", qr);
                var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    return Ok(new {
                        status   = "QR_OK",
                        username = reader["username"].ToString(),
                        faceData = reader["face_data"].ToString()
                    });
                }
            }
            return Unauthorized(new { error = "Invalid or unrecognized QR code" });
        }

        // ─── LOGIN STEP 3 — FACE ─────────────────────────────────
        [HttpPost("login/face")]
        public IActionResult LoginFace([FromBody] FaceLoginRequest req)
        {
            if (string.IsNullOrEmpty(req.StoredFace))
                return Ok(new { status = "ACCESS_GRANTED" });
            return Ok(new { status = "ACCESS_GRANTED" });
        }

        // ─── APPROVE USER (admin) ─────────────────────────────────
        [HttpPost("approve/{username}")]
        public IActionResult Approve(string username)
        {
            using (var conn = db.GetConnection())
            {
                conn.Open();
                var cmd = new MySqlCommand(
                    "UPDATE users SET is_approved=1 WHERE username=@u", conn);
                cmd.Parameters.AddWithValue("@u", username);
                int affected = cmd.ExecuteNonQuery();

                if (affected == 0)
                    return NotFound(new { error = "User not found" });
            }
            return Ok(new { message = $"{username} approved ✅" });
        }

        // ─── REJECT USER (admin) ──────────────────────────────────
        [HttpPost("reject/{username}")]
        public IActionResult Reject(string username)
        {
            using (var conn = db.GetConnection())
            {
                conn.Open();
                var cmd = new MySqlCommand(
                    "UPDATE users SET is_approved=0 WHERE username=@u", conn);
                cmd.Parameters.AddWithValue("@u", username);
                int affected = cmd.ExecuteNonQuery();

                if (affected == 0)
                    return NotFound(new { error = "User not found" });
            }
            return Ok(new { message = $"{username} rejected" });
        }

        // ─── DELETE USER (admin) ──────────────────────────────────
        [HttpDelete("delete/{username}")]
        public IActionResult Delete(string username)
        {
            using (var conn = db.GetConnection())
            {
                conn.Open();
                var cmd = new MySqlCommand(
                    "DELETE FROM users WHERE username=@u", conn);
                cmd.Parameters.AddWithValue("@u", username);
                int affected = cmd.ExecuteNonQuery();

                if (affected == 0)
                    return NotFound(new { error = "User not found" });
            }
            return Ok(new { message = $"{username} deleted" });
        }

        // ─── RESET FACE (admin) ───────────────────────────────────
        [HttpPost("reset-face/{username}")]
        public IActionResult ResetFace(string username)
        {
            using (var conn = db.GetConnection())
            {
                conn.Open();
                var cmd = new MySqlCommand(
                    "UPDATE users SET face_data='' WHERE username=@u", conn);
                cmd.Parameters.AddWithValue("@u", username);
                cmd.ExecuteNonQuery();
            }
            return Ok(new { message = $"Face data cleared for {username}" });
        }

        // ─── QR GENERATOR HELPER ──────────────────────────────────
        private string GenerateQR(string text)
        {
            using (var qrGen = new QRCodeGenerator())
            {
                var data   = qrGen.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new Base64QRCode(data);
                return qrCode.GetGraphic(20);
            }
        }
    }

    // ─── REQUEST MODELS ───────────────────────────────────────────
    public class RegisterRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string FaceData { get; set; } = "";
    }

    public class LoginRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class SaveFaceRequest
    {
        public string Username { get; set; } = "";
        public string FaceData { get; set; } = "";
    }

    public class FaceLoginRequest
    {
        public string InputFace  { get; set; } = "";
        public string StoredFace { get; set; } = "";
    }
}