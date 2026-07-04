using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using RiderIntercom.Exceptions;
using RiderIntercom.Hubs;
using RiderIntercom.Interfaces;
using RiderIntercom.Services;

namespace RiderIntercom
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AppContext.SetSwitch("System.Net.DisableIPv6", true);

            var builder = WebApplication.CreateBuilder(args);
            var jwtKey = builder.Configuration["Jwt:Key"];

            // Add services to the container.
            builder
                .Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    };

                    // 🔥 IMPORTANT for SignalR
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;

                            if (
                                !string.IsNullOrEmpty(accessToken)
                                && path.StartsWithSegments("/hub")
                            )
                            {
                                context.Token = accessToken;
                            }

                            return Task.CompletedTask;
                        },
                    };
                });

            builder.Services.AddAuthorization();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy(
                    "AllowAngular",
                    policy =>
                        policy
                            .WithOrigins(
                                "http://localhost:4200",
                                "https://rider-intercom-ui.vercel.app",
                                "https://rider-intercom-ui-suraj5869s-projects.vercel.app",
                                "https://localhost"
                            )
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials()
                );
            });

            builder.Services.AddSignalR();
            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddScoped<IDbConnectionFactory, DbConnectionFactory>();
            builder.Services.AddScoped<AuthRepository>();
            builder.Services.AddScoped<RoomRepository>();
            builder.Services.AddScoped<UserRepository>();
            builder.Services.AddScoped<JwtService>();
            builder.Services.AddScoped<PlaylistRepository>();
            builder.Services.AddScoped<ClaudinaryService>();
            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50 MB
            });
            var app = builder.Build();

            app.UseRouting();
            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.UseCors("AllowAngular");
            app.UseHttpsRedirection();

            app.UseMiddleware<ExceptionMiddleware>();
            app.UseAuthentication();
            app.UseAuthorization();
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<RiderHub>("/rideHub");
            });
            var port = Environment.GetEnvironmentVariable("PORT");
            if (!string.IsNullOrEmpty(port))
            {
                app.Urls.Add($"http://0.0.0.0:{port}");
            }

            app.Run();
        }
    }
}
