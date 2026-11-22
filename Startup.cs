using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using ShoppingListServer.Database;
using ShoppingListServer.Helpers;
using ShoppingListServer.Services;
using ShoppingListServer.LiveUpdates;
using ShoppingListServer.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Npgsql;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.OpenApi.Models;

namespace ShoppingListServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            Environment = env;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; }

        public static ServiceProvider _serviceProvider;


        // This method gets called by the runtime. Use this method to add services to the container.
        // NOTE: ASP.NET Core requires either no parameters or a single IServiceCollection parameter.
        // The hosting environment is available through the Environment property injected into the constructor.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();
            services.AddControllers();
            services.AddEndpointsApiExplorer();

            // Replaced asp .net core 2.0 AddMvc()
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            services.AddControllersWithViews();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Shopping List Server API",
                    Version = "v1",
                    Description = "HTTP API for user, shopping list, verification, and password-reset workflows."
                });

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\""
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // Swagger / OpenAPI documentation, available at /swagger
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ShoppingList API",
                    Version = "v1",
                    Description = "REST API of the ShoppingList / ShoppingNow server."
                });

                // Pick up the XML comments written in the controllers and models.
                // Wrapped in try/catch: a malformed doc comment must never break
                // the whole OpenAPI document.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    try
                    {
                        options.IncludeXmlComments(xmlPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Warning: Could not read XML documentation for Swagger: " + ex.Message);
                    }
                }

                // JWT bearer authentication: allows "Authorize" in the Swagger UI.
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter the JWT returned by POST /Users/authenticate."
                });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // configure strongly typed settings objects
            var appSettingsSection = Configuration.GetSection("AppSettings");
            services.Configure<AppSettings>(appSettingsSection);

            // configure jwt authentication
            var appSettings = appSettingsSection.Get<AppSettings>();
            var key = Encoding.ASCII.GetBytes(appSettings.Secret);
            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false
                };

                // API Key AUTH.
                
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];

                        // If the request is on update hub
                        // TO DO get rout from config
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/shoppingserver/update")))
                        {
                            // Read the token out of the query string
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };

           });

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            services.AddSignalR();

            // configure DI for application services
            // Scoped services are created once per request.
            services.AddScoped<IFilesystemService, FilesystemService>();
            services.AddScoped<IShoppingListStorageService, ShoppingListStorageSevice>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IRestService, RestService>();
            services.AddScoped<IEMailVerificationService, EMailVerificationService>();
            services.AddScoped<IResetPasswordService, ResetPasswordService>();
            services.AddScoped<IShoppingService, ShoppingService>();
            // Transient services are created each time they are requested.
            services.AddTransient<IUserHub, UserHubService>();
            services.AddTransient<IShoppingHub, ShoppingHubService>();
            services.AddTransient<IPushNotificationService, PushNotificationService>();
            services.AddTransient<IAuthenticationService, AuthenticationService>();

            //_serviceProvider = services.BuildServiceProvider();


            // PostgreSQL database
            // Npgsql.EntityFrameworkCore.PostgreSQL: https://www.npgsql.org/efcore/
            // A PostgreSQL database must be setup in the system with a name and user access specified in appsettings.json
            string dbServerAddress = appSettings.UseDocker == "False" ? appSettings.DbServerAddress : appSettings.DbServerAddressDocker;

            // Build the connection string with the strongly typed builder so that
            // values containing semicolons or special characters are escaped.
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(dbServerAddress)
            {
                Username = appSettings.DbUser,
                Password = appSettings.DbPassword,
                Database = appSettings.DbName,
            };
            string connectionString = connectionStringBuilder.ConnectionString;

            // Console.WriteLine("Database connection string = " + connectionString);

            services.AddDbContextPool<AppDb>(
                options => {
                    options.UseLazyLoadingProxies();
                    bool useSQLite = Configuration.GetValue<bool>("UseSQLite");
                    if (useSQLite)
                    {
                        options.UseSqlite("Data Source=shoppinglist.db");
                    }
                    else
                    {
                        options.UseNpgsql(
                            connectionString,
                            npgsqlOptions =>
                            {
                                // The API starts before PostgreSQL is necessarily
                                // accepting connections; retry instead of crashing.
                                npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
                            });
                    }
#if DEBUG
                    options.EnableDetailedErrors();
#endif
  
                    // Enable sensitive-data logging only when explicitly enabled in configuration.
                    var enableSensitiveLogging = Configuration.GetValue<bool>("AppSettings:EnableSensitiveDataLogging", false);
                    if (Environment.IsDevelopment() && enableSensitiveLogging)
                    {
                        options.EnableSensitiveDataLogging();
                    }
                });

            Console.WriteLine("Network adapter prioritization:");
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up)
                {
                    Console.WriteLine(ni.NetworkInterfaceType.ToString());
                }
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // All non handled exceptions in the controllers are handled by simply responding with the exceptions message.
            // https://stackoverflow.com/a/55166404
            // https://stackoverflow.com/a/38935583
            app.UseExceptionHandler(c => c.Run(async context =>
            {
                var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
                var exception = exceptionHandlerPathFeature.Error;

                // Setting the reason phrase: https://stackoverflow.com/a/42039124
                context.Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = exception.Message;
                context.Response.ContentType = "application/json";
                await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(exception.Message));
            }));

            // Handle all other non cached exceptions
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(Unhandled_Exceptions);

            // global cors policy
            app.UseCors(x => x
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());

            app.UseRouting();

            // Swagger UI:            /swagger
            // OpenAPI document:      /swagger/v1/swagger.json
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "ShoppingList API v1");
                options.RoutePrefix = "swagger";
                options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
            });

            app.UseAuthentication();
            app.UseAuthorization();

            // Server accessibility on browser routes
            // Define new StaticFileOptions to enable access of files with no ending (apple-app-site-association)
            app.UseStaticFiles(new StaticFileOptions
            {
                ServeUnknownFileTypes = true,
                DefaultContentType = "text/plain"
            });

            var appSettingsSection = Configuration.GetSection("AppSettings");
            var appSettings = appSettingsSection.Get<AppSettings>();
            if (appSettings.UseHttpsRedirect == "True")
                app.UseHttpsRedirection();
#if DEBUG
            // This overwrites the exception handler from the call app.UseExceptionHandler() above.
            //app.UseDeveloperExceptionPage();
            app.UseHsts();
#else
            app.UseHsts();
#endif

            try
            {
                bool useFirebase = Configuration.GetValue<bool>("UseFirebase");
                if (useFirebase)
                {
                    FirebaseApp.Create(new AppOptions()
                    {
                        Credential = GoogleCredential.GetApplicationDefault(),
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: Failed to initialize Firebase.\n" + ex.StackTrace);
            }
            


            // SignalR/Websockets
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<UpdateHub_Controller>("/shoppingserver/update");
                endpoints.MapControllers();
            });

            Console.WriteLine("Apply Database Migrations.");
            // apply migrations
            using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
            {
                var context = serviceScope.ServiceProvider.GetRequiredService<AppDb>();
                context.Database.Migrate();
            }
        }

        private void Unhandled_Exceptions(object sender, UnhandledExceptionEventArgs e)
        {
            // Console.WriteLine("Unhandled Exception:" + (e.ExceptionObject as Exception).Message);
        }
    }
}
