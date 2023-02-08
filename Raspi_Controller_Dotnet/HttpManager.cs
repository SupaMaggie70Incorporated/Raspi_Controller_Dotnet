using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

using System.Net;
using System.Net.Http;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json.Nodes;
using System.Linq.Expressions;
using System.Security.AccessControl;

namespace Raspi_Controller_Dotnet
{
    public class HttpManager
    {
        public IPHostEntry MyIP;

        public HttpListener Server;



        public HttpManager()
        {
            Server = new HttpListener();
            MyIP = Dns.GetHostEntry(Dns.GetHostName());
            Server.Prefixes.Add("http://+:80/");
            Server.Start();
            Server.BeginGetContext(Accept, null);
        }

        private void Accept(IAsyncResult ar)
        {
            Server.BeginGetContext(Accept, null);
            try
            {
                HttpListenerContext ctx = Server.EndGetContext(ar);
                if (Program.FilterPrivateHttp && !IsPrivate(ctx.Request.RemoteEndPoint.Address))
                {
                    BannedIP(ctx);
                    return;
                }
                string resource = ctx.Request.RawUrl;
                string[] resourceParts = resource.Split('/');
                if (resource[resource.Length - 1] == '/' && resource.Length >= 2) resource = resource.Substring(1, resource.Length - 2);
                else resource = resource.Substring(1, resource.Length - 1);
                Cookie AuthCookie = ctx.Request.Cookies["authtoken"];
                JsonNode User;
                if (AuthCookie == null) User = null;
                else User = Program.UserManager.GetUserFromToken(AuthCookie.Value);

                if (resource.StartsWith("raw/"))
                {
                    if (resource.Contains(".."))
                    {
                        ctx.Response.OutputStream.Write(Encoding.UTF8.GetBytes("File paths containing \"..\" are not allowed."));
                        ctx.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        ctx.Response.Close();
                        return;
                    }
                    string file = resource.Substring(4);
                    string fileLower = file.ToLower();
                    string fp = $"{Program.HtmlPath}/{file}";
                    if (!File.Exists(fp))
                    {
                        ctx.Response.OutputStream.Write(Encoding.UTF8.GetBytes("File not found."));
                        ctx.Response.OutputStream.Flush();
                        ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        ctx.Response.Close();
                        return;
                    }
                    FileStream fs = File.OpenRead(fp);
                    fs.CopyTo(ctx.Response.OutputStream);
                    ctx.Response.OutputStream.Flush();
                    if (fileLower.EndsWith(".html")) ctx.Response.ContentType = "text/html";
                    else if (fileLower.EndsWith(".js")) ctx.Response.ContentType = "application/javascript";
                    else if (fileLower.EndsWith(".css")) ctx.Response.ContentType = "text/css";
                    else if (fileLower.EndsWith(".gif")) ctx.Response.ContentType = "image/gif";
                    else if (fileLower.EndsWith(".png")) ctx.Response.ContentType = "image/png";
                    else if (fileLower.EndsWith(".jpg") || fileLower.EndsWith(".jpeg")) ctx.Response.ContentType = "image/jpeg";
                    fs.Close();
                    ctx.Response.Close();
                    return;
                }
                else if (resource == "logo.png")
                {
                    Console.WriteLine("Sending logo");
                    FileStream fs = File.OpenRead(Program.HtmlPath + "/logo.png");
                    fs.CopyTo(ctx.Response.OutputStream);
                    fs.Close();
                    ctx.Response.ContentType = "image/png";
                    ctx.Response.Close();
                    return;
                }
                else if (resource == "login")
                {
                    Console.WriteLine("Sending login form");
                    FileStream fs = File.OpenRead(Program.HtmlPath + "/login.html");
                    fs.CopyTo(ctx.Response.OutputStream);
                    fs.Close();
                    ctx.Response.ContentType = "text/html";
                    ctx.Response.Close();
                    return;
                }
                else if (resource == "login_fail")
                {
                    Console.WriteLine("Sending invalid login page");
                    FileStream fs = File.OpenRead(Program.HtmlPath + "/login_fail.html");
                    fs.CopyTo(ctx.Response.OutputStream);
                    fs.Close();
                    ctx.Response.ContentType = "text/html";
                    ctx.Response.Close();
                    return;
                }
                else if (resource == "authorize")
                {
                    string body = new StreamReader(ctx.Request.InputStream).ReadToEnd();
                    Console.WriteLine(body);
                    string[] parts = body.Split('&');
                    string username = "";
                    string password = "";

                    foreach (string part in parts)
                    {
                        int index = part.IndexOf('=');
                        if (index == -1 || index == part.Length - 1 || index == 0) continue;
                        string key = part.Substring(0, index);
                        string value = part.Substring(index + 1);
                        if (key == "username") username = value;
                        else if (key == "password") password = value;
                    }
                    try
                    {
                        JsonNode user = Program.UserManager.GetUser(username);
                        if (user == null) Console.WriteLine($"No user found with username {username}");
                        if (user["hash"].GetValue<string>() == UserManager.PasswordHash(password))
                        {
                            ctx.Response.AppendCookie(new Cookie("authtoken", Program.UserManager.MakeToken(username, user)));
                            ctx.Response.Redirect("/home");
                            ctx.Response.Close();
                        }
                        else
                        {
                            ctx.Response.Redirect("/login_fail");
                            ctx.Response.Close();
                        }
                    }
                    catch
                    {
                        ctx.Response.Redirect("/login_fail");
                        ctx.Response.Close();
                    }
                    return;
                }
                else if (AuthCookie == null || Program.UserManager.GetUserFromToken(AuthCookie.Value) == null)
                {
                    ctx.Response.Redirect("/login");
                    ctx.Response.Close();
                }
                else if (resource == "home")
                {
                    Console.WriteLine("Sending home page");
                    FileStream fs = File.OpenRead(Program.HtmlPath + "/home.html");
                    fs.CopyTo(ctx.Response.OutputStream);
                    fs.Close();
                    ctx.Response.ContentType = "text/html";
                    ctx.Response.Close();
                    return;
                }
                else if (resource == "files")
                {
                    if(!Program.UserManager.UserHasPerms(User, "viewfiles"))
                    {
                        NotAllowedPage(ctx); 
                        return;
                    }
                    Console.WriteLine("Sending files page");
                    FileStream fs = File.OpenRead(Program.HtmlPath + "/files.html");
                    fs.CopyTo(ctx.Response.OutputStream);
                    fs.Close();
                    ctx.Response.ContentType = "text/html";
                    ctx.Response.Close();
                    return;
                }
                else if(resource.StartsWith("files/open/"))
                {
                    if (!Program.UserManager.UserHasPerms(User, "readfiles"))
                    {   
                        NotAllowedPage(ctx);
                        return;
                    }
                    Console.WriteLine("Sending file editor page");
                    FileStream fs = File.OpenRead(Program.HtmlPath + "/fileeditor.html");
                    fs.CopyTo(ctx.Response.OutputStream);
                    fs.Close();
                    ctx.Response.ContentType = "text/html";
                    ctx.Response.Close();
                    return;
                }
                else if (resource.StartsWith("files/read/"))
                {
                    if (!Program.UserManager.UserHasPerms(User, "readfiles"))
                    {
                        NotAllowedPage(ctx);
                        return;
                    }
                    string path = resource.Substring(11).Replace("%20", " ");
                    string filename = Path.GetFileName(path);
                    bool prot = Program.FileManager.IsProtected(path);
                    if (prot && !Program.UserManager.UserHasPerms(User, "accessprotectedfiles"))
                    {
                        NotAllowedPage(ctx);
                        return;
                    }
                    FileStream fs = File.OpenRead(path);
                    fs.CopyTo(ctx.Response.OutputStream);
                    fs.Close();
                    ctx.Response.Headers.Set("Content-Type", "application/octet-stream");
                    ctx.Response.Headers.Set("Content-Disposition", $"attachment; filename=\"{filename}\"");
                    ctx.Response.Close();
                    return;
                }
                else if (resource.StartsWith("files/download-zip/"))
                {
                    if (!Program.UserManager.UserHasPerms(User, "readfiles"))
                    {
                        NotAllowedPage(ctx);
                        return;
                    }
                    string path = resource.Substring(19).Replace("%20", " ");
                    string dir = Path.GetFileName(path);
                    bool prot = Program.FileManager.IsProtected(path);
                    
                    if (prot && !Program.UserManager.UserHasPerms(User, "accessprotectedfiles"))
                    {
                        NotAllowedPage(ctx);
                        return;
                    }
                    bool viewProtected = Program.UserManager.UserHasPerms(User, "accessprotectedfiles");
                    ctx.Response.Headers.Set("Content-Type", "application/octet-stream");
                    ctx.Response.Headers.Set("Content-Disposition", $"attachment; filename=\"{dir}.zip\"");
                    Program.FileManager.ZipFileTo(dir, ctx.Response.OutputStream, viewProtected);
                    ctx.Response.Close();
                    return;
                }
                else if (resource.StartsWith("files/list/"))
                {
                    if (!Program.UserManager.UserHasPerms(User, "viewfiles"))
                    {
                        NotAllowedPage(ctx);
                        return;
                    }
                    string path = resource.Substring(11).Replace("%20", " ") + '/';


                    bool viewProt = Program.UserManager.UserHasPerms(User, "accessprotectedfiles");
                    Console.WriteLine($"Sending {(viewProt ? "protected" : "unprotected")} file & directory list for {path}");
                    JsonArray directories = new JsonArray(Directory.GetDirectories(path).Where(dir => !Program.FileManager.IsProtected(dir) || viewProt).Select(dir => JsonValue.Create(Path.GetFileName(dir))).ToArray());
                    JsonArray files = new JsonArray(Directory.GetFiles(path).Where(file => !Program.FileManager.IsProtected(file) || viewProt).Select(file => JsonValue.Create(Path.GetFileName(file))).ToArray());
                    JsonObject obj = new JsonObject() {
                        { "directories", directories},
                        { "files", files}
                    };
                    Console.WriteLine("Sending ");
                    Console.WriteLine(obj.ToJsonString());
                    //obj.WriteTo(new System.Text.Json.Utf8JsonWriter(ctx.Response.OutputStream));
                    ctx.Response.OutputStream.Write(Encoding.UTF8.GetBytes(obj.ToJsonString()));
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.Close();
                    return;
                }
                else if (resource.StartsWith("files/write/"))
                {
                    if (!Program.UserManager.UserHasPerms(User, "writefiles"))
                    {
                        NotAllowedPage(ctx);
                        return;
                    }
                    string path = resource.Substring(12).Replace("%20", " ");
                    bool prot = Program.FileManager.IsProtected(path);
                    Console.WriteLine($"Writing to {path}");
                    if (prot && !Program.UserManager.UserHasPerms(User, "accessprotectedfiles"))
                    {
                        NotAllowedPage(ctx);
                        return;
                    }
                    FileStream fs = File.OpenWrite(path);
                    ctx.Request.InputStream.CopyTo(fs);
                    fs.Flush();
                    fs.Dispose();
                    ctx.Response.Close();
                }
                else if (resource.StartsWith("files/delete/"))
                {
                    if (!Program.UserManager.UserHasPerms(User, "deletefiles"))
                    {
                        NotAllowedPage(ctx);
                        return;
                    }
                    string path = "C:/" + resource.Substring(13).Replace("%20", " ");
                    bool prot = Program.FileManager.IsProtected(path);
                    if (prot && !Program.UserManager.UserHasPerms(User, "deleteprotectedfiles"))
                    {
                        NotAllowedPage(ctx);
                        return;
                    }
                    FileAttributes attr = File.GetAttributes(path);
                    bool isDir = (attr & FileAttributes.Directory) == FileAttributes.Directory;
                    if (!isDir) File.Delete(path);
                    else Directory.Delete(path, true);
                    ctx.Response.OutputStream.Write(Encoding.UTF8.GetBytes("success"));
                    ctx.Response.Close();
                }
                else if (resource == "network")
                {
                    if (!Program.UserManager.UserHasPerms(User, "viewnetwork"))
                    {
                        NotAllowedPage(ctx);
                        return;
                    }
                    Console.WriteLine("Sending network manager");
                    FileStream fs = File.OpenRead(Program.HtmlPath + "/network_viewer.html");
                    fs.CopyTo(ctx.Response.OutputStream);
                    fs.Close();
                    ctx.Response.ContentType = "text/html";
                    ctx.Response.Close();
                    return;
                }
                else if (resource == "network-devices-as-html")
                {
                    if (!Program.UserManager.UserHasPerms(User, "viewnetwork"))
                    {
                        NotAllowedPage(ctx);
                        return;
                    }
                    string constantHeader = "<tr><th>Device Name</th><th>Device Address</th><th>Last Seen</th></tr>";
                    byte[] b = Encoding.UTF8.GetBytes(constantHeader);
                    ctx.Response.OutputStream.Write(b, 0, b.Length);
                    /*
                    foreach(JsonNode node in Program.NetworkManager.JSON["devices"].AsArray())
                    {
                        ctx.Response.OutputStream.Write(Encoding.ASCII.GetBytes($"<td>{node}</td><td>{node}</td>{node}<td>{node}</td>"));
                    }*/
                    ctx.Response.OutputStream.Write(Encoding.ASCII.GetBytes("<td>Service Offline</td><td>Service Offline</td><td>Service Offline</td>"));
                    ctx.Response.Close();
                    return;
                }
                else if (resource == "")
                {
                    ctx.Response.Redirect("/home/");
                    ctx.Response.Close();
                    return;
                }
                else
                {
                    Console.WriteLine("Sending 404 page");
                    FileStream fs = File.OpenRead(Program.HtmlPath + "/404notfound.html");
                    fs.CopyTo(ctx.Response.OutputStream);
                    fs.Close();
                    ctx.Response.ContentType = "text/html";
                    ctx.Response.Close();
                    return;
                }
                ctx.Response.Close();
            }
            catch(Exception e)
            {
                
                Console.WriteLine("Error in http request handling: ");
                Console.WriteLine(e.ToString());
                // throw;
            }
        }
        public void NotAllowedPage(HttpListenerContext ctx)
        {
            Console.WriteLine("Sending not allowed page");
            FileStream fs = File.OpenRead(Program.HtmlPath + "/notallowed.html");
            fs.CopyTo(ctx.Response.OutputStream);
            fs.Close();
            ctx.Response.ContentType = "text/html";
            ctx.Response.Close();
            return;
        }
        private void BannedIP(HttpListenerContext ctx)
        {
            ctx.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            FileStream fs = File.OpenRead(Program.HtmlPath + "\\ipbanned.html");
            fs.CopyTo(ctx.Response.OutputStream);
            fs.Close();
            ctx.Response.Close();
        }
        public static bool IsPrivate(IPAddress addr)
        {
            byte[] bytes = addr.GetAddressBytes();
            return (bytes[0] == 10) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 172 && (bytes[1] >= 16 && bytes[1] <= 31));
        }
    }
}
