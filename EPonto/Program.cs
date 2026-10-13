using Application.Interfaces;
using Application.Jobs;
using Application.Services;
using Data.Connections;
using Data.Interfaces;
using Data.Repositories;
using Hangfire;
using Hangfire.MySql;
using Hangfire.Storage;
using CloudinaryDotNet;
using Microsoft.Extensions.Options;
using Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using Domain.Entities.Login;
using System.Reflection;
using Swashbuckle.AspNetCore.Filters;
using Domain.Entities.Feriado_e_Ferias;
using System.Security.Claims;
using EPonto.Demo;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Origens liberadas no CORS: Cors__Origens__0, Cors__Origens__1… (sem configuração, só o front local)
var origensCors = builder.Configuration.GetSection("Cors:Origens").Get<string[]>() is { Length: > 0 } origens
    ? origens
    : new[] { "http://localhost:3000", "http://localhost:8080" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTudo", policy =>
    {
        policy.WithOrigins(origensCors)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Se estiver usando cookies/autenticação
    });
});


//Propiedade para fazer mapemaneto de entidades com nome no banco diferente
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(x =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    x.IncludeXmlComments(xmlPath);

    x.ExampleFilters(); // habilita leitura dos exemplos

    x.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        In = ParameterLocation.Header,
    });

    x.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new string[] {}
        }
    });
});

builder.Services.AddSwaggerExamplesFromAssemblyOf<FeriasRequestExampleModel>();// registra exemplos deste assembly

//Login
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddSingleton<TentativasRecuperacaoSenha>();
builder.Services.AddScoped<ILoginRepository, LoginRepository>();

//Feriado 
builder.Services.AddScoped<IFeriadoService, FeriadoService>();
builder.Services.AddScoped<IFeriadoRepository, FeriadoRepository>();

//Ferias
builder.Services.AddScoped<IFeriasService, FeriasService>();
builder.Services.AddScoped<IFeriasRepository, FeriasRepository>();

//Calendario 
builder.Services.AddScoped<ICalendarioService, CalendarioService>();

//Comunicado
builder.Services.AddScoped<IComunicadoService, ComunicadoService>();
builder.Services.AddScoped<IComunicadoRepository, ComunicadoRepository>();

//Usuario
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();

//Ponto
builder.Services.AddScoped<IRegistroPontoService, RegistroPontoService>();
builder.Services.AddScoped<IRegistroPontoRepository, RegistroPontoRepository>();

//Cargo
builder.Services.AddScoped<ICargoService, CargoService>();
builder.Services.AddScoped<ICargoRepository, CargoRepository>();

//Jornada de trabalho
builder.Services.AddScoped<IJornadaTrabalhoService, JornadaTrabalhoService>();
builder.Services.AddScoped<IJornadaTrabalhoRepository, JornadaTrabalhoRepository>();

//Banco Horas
builder.Services.AddScoped<IBancoHorasService, BancoHorasService>();
builder.Services.AddScoped<IBancoHorasRepository, BancoHorasRepository>();

//Feedback
builder.Services.AddScoped<IFeedbackService, FeedbackService>();
builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();

//SolicitacaoAusencia
builder.Services.AddScoped<ISolicitacaoAusenciaService, SolicitacaoAusenciaService>();
builder.Services.AddScoped<ISolicitacaoAusenciaRepository, SolicitacaoAusenciaRepository>();

//PERFIL DE USUÁRIO
builder.Services.AddScoped<IPerfilService, PerfilService>();
builder.Services.AddScoped<IPerfilRepository, PerfilRepository>();

//PERFIL DE USUÁRIO
builder.Services.AddScoped<IRelatorioService, RelatorioService>();
builder.Services.AddScoped<IRelatorioRepository, RelatorioRepository>();

builder.Services.AddScoped<DbSession>();

// Hangfire - Configuração com MySQL
builder.Services.AddHangfire(configuration =>
{
    configuration.UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UseStorage(new MySqlStorage(
                        builder.Configuration.GetConnectionString("HangfireConnection"),
                        new MySqlStorageOptions
                        {
                            TablesPrefix = "Hangfire"
                        }));
});

builder.Services.AddHangfireServer();

// Adicione o serviço do job
builder.Services.AddScoped<BancoHorasJob>();

// Modo demonstração (Demo__Ativo=true no deploy público): limites de gravação,
// contas de teste protegidas e restauração diária dos dados de exemplo
builder.Services.Configure<ModoDemoOptions>(builder.Configuration.GetSection("Demo"));
builder.Services.AddScoped<RestaurarDemoJob>();
builder.Services.AddLimitesDemo();

// Atrás do proxy do Railway: o IP do cliente é o último valor do X-Forwarded-For
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("Cloudinary"));

builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IOptions<CloudinarySettings>>().Value;
    var account = new Account(cfg.CloudName, cfg.ApiKey, cfg.ApiSecret);
    var cloudinary = new Cloudinary(account) { Api = { Secure = true } };
    return cloudinary;
});
builder.Services.AddSingleton(sp => new Lazy<Cloudinary>(() => sp.GetRequiredService<Cloudinary>()));

// Sua abstração de storage
builder.Services.AddScoped<ICloudStorage, CloudinaryStorage>();

// A chave vem de Jwt__Key (variável de ambiente); sem ela a API não sobe
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Configure Jwt__Key com pelo menos 32 caracteres (variável de ambiente).");

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        // O token é gerado com Issuer e Audience (LoginService); agora eles são conferidos
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        RoleClaimType = ClaimTypes.Role,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };

    // Evento personalizado para tratar falhas de autenticação
    x.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            // Previne o comportamento padrão
            context.HandleResponse();

            // Define o status code
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";

            // Resposta personalizada
            var response = new
            {
                Sucesso = false,
                Mensagem = "Você precisa estar autenticado para acessar este método. Por favor, realize o login no sistema."
            };

            return context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        },

        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
            {
                Sucesso = false,
                message = "Acesso negado. Você não possui permissão de Administrador."
            }));
        },
    };
});

builder.Services.Configure<JwtSettingsModel>(builder.Configuration.GetSection("Jwt"));


var app = builder.Build();

app.UseForwardedHeaders();

// Sempre habilita o Swagger (tanto em DEV quanto em produção)
app.UseSwagger();
app.UseSwaggerUI();

// Redireciona a raiz "/" para o Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));


app.UseHttpsRedirection();

app.UseCors("PermitirTudo");

// Explícito e depois do CORS (antes o ASP.NET inseria a autenticação sozinho no início)
app.UseAuthentication();
app.UseAuthorization();

// Depois da autenticação: o limite por usuário lê o id do token
app.UseRateLimiter();
app.UseProtecoesDemo();

app.MapControllers();

app.UseHangfireDashboard(); // Se quiser dashboard do Hangfire

// Agendamento de job com Hangfire. Sem o fuso, o Hangfire usa UTC e o
// "20:30" rodava às 17:30 de Brasília.
var fusoBrasilia = new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo") };
RecurringJob.AddOrUpdate<BancoHorasJob>(
    "processar-banco-horas-diario",
    job => job.ExecutarProcessamentoDiario(),
    "30 20 * * *", // Cron: todo dia às 20:30
    fusoBrasilia
);

var modoDemo = app.Services.GetRequiredService<IOptions<ModoDemoOptions>>().Value;
if (modoDemo.Ativo)
{
    RecurringJob.AddOrUpdate<RestaurarDemoJob>("restaurar-demonstracao", job => job.Restaurar(), modoDemo.CronRestauracao, fusoBrasilia);
    BackgroundJob.Enqueue<RestaurarDemoJob>(job => job.RestaurarSeVazio());
}
else
{
    RecurringJob.RemoveIfExists("restaurar-demonstracao");
}

app.Run();