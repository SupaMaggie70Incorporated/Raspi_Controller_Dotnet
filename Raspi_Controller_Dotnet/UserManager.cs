using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace Raspi_Controller_Dotnet
{
    public class UserManager
    {
        public JsonNode JSON;
        public List<AuthToken> AuthTokens;
        public JsonNode AnonymousUser;

        public JsonNode GetUserFromToken(string token)
        {
            if(token == "-1")
            {
                return AnonymousUser;
            }
            for (int i = 0; i < AuthTokens.Count; i++)
            {
                if (AuthTokens[i].Expired())
                {
                    AuthTokens.RemoveAt(i);
                    i--;
                    continue;
                }
                if (AuthTokens[i].Token == token)
                {
                    return AuthTokens[i].User;
                }
            }
            return null;
        }
        public string MakeToken(string username, JsonNode user)
        {
            AuthTokens.Add(new AuthToken(username, user));
            return AuthTokens[AuthTokens.Count - 1].Token;
        }
        public UserManager() {
            bool loaded = false;
            AuthTokens = new List<AuthToken>();
            if (File.Exists("users.json"))
            {
                FileStream fs = File.OpenRead("users.json");
                try
                {
                    JSON = JsonNode.Parse(fs);
                    loaded = true;
                }
                catch { }
                fs.Close();
            }
            if(!loaded)
            {
                JSON = new JsonObject();
                JSON["users"] = new JsonArray();
                JSON["permissions"] = new JsonObject();
                MakeUser("admin", "admin", 99, new string[0]);
                Save();
            }
            foreach(JsonNode node in JSON["users"].AsArray())
            {
                Console.WriteLine($"User: {node.ToJsonString()}");
            }
            SetupAnonymousUser();
        }
        public void SetupAnonymousUser()
        {
            try
            {
                JsonObject user = new JsonObject();
                user["username"] = JsonValue.Create("Anonymous");
                user["hash"] = JsonValue.Create(PasswordHash(""));
                user["accesslevel"] = JsonValue.Create(0);
                user["additionalpermissions"] = new JsonArray();
                AnonymousUser = user;
            }
            catch { }
        }
        public void Save()
        {
            Console.WriteLine("Saving");
            File.SetAttributes("users.json", FileAttributes.System);
            File.SetAttributes("users.json", FileAttributes.Hidden);
            FileStream fs = File.OpenWrite("users.json");
            JSON.WriteTo(new System.Text.Json.Utf8JsonWriter(fs));
            fs.Flush();
            fs.Dispose();
        }
        public static string PasswordHash(string password)
        {
            return BitConverter.ToString(Encoding.UTF8.GetBytes(password));
        }
        public JsonObject GetUser(string name)
        {
            JsonArray arr = JSON["users"].AsArray();
            foreach(JsonObject n in arr)
            {
                if (n["username"].GetValue<string>() == name)
                {
                    return n;
                }
            }
            return null;
        }
        public JsonObject MakeUser(string name, string password, int accessLevel, string[] additionalPermissions)
        {
            if (GetUser(name) != null) return null;
            try
            {
                JsonArray arr = JSON["users"].AsArray();
                JsonObject user = new JsonObject();
                user["username"] = JsonValue.Create(name);
                user["hash"] = JsonValue.Create(PasswordHash(password));
                user["accesslevel"] = JsonValue.Create(accessLevel);
                JsonArray permsArray = new JsonArray();
                foreach (string perm in additionalPermissions) permsArray.Add(JsonValue.Create(perm));
                user["additionalpermissions"] = permsArray;
                JSON["users"].AsArray().Add(user);
                Save();
                return user;
            }
            catch { return null; }
        }
        public bool UserHasPerms(JsonNode user, string perm)
        {
            int userAccessLevel = user["accesslevel"].GetValue<int>();
            if (userAccessLevel >= 99) return true;
            if (user["additionalpermissions"].AsArray().Contains(perm)) return true;
            JsonObject permsObject = JSON["permissions"].AsObject();
            if (!permsObject.ContainsKey(perm)) return false;
            return permsObject[perm]["accesslevel"].GetValue<int>() <= userAccessLevel;
        }
    }
    public struct AuthToken
    {
        public static Random Random = new Random();
        public string Username;
        public JsonNode User;
        public string Token;
        public DateTime Expiration;


        public AuthToken(string username, JsonNode user)
        {
            Username = username;
            User = user;
            Expiration = DateTime.UtcNow.AddDays(1);
            byte[] buffer = new byte[32];
            Random.NextBytes(buffer);
            Token = BitConverter.ToString(buffer, 0, buffer.Length);
        }
        public bool Expired()
        {
            return DateTime.UtcNow.Ticks > Expiration.Ticks;
        }
    }
}
