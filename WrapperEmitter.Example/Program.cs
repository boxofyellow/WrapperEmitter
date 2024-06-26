﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Octokit;

namespace WrapperEmitter.Example;

public class Program
{
    public static async Task Main(string[] args)
    {
        var category = typeof(Program).FullTypeExpression();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddFilter(category, LogLevel.Debug)
                  .AddConsole();
        });
        var logger = loggerFactory.CreateLogger(category);

        string token = args.Length > 0
            ? args.First()
            : string.Empty;

        if (string.IsNullOrEmpty(token))
        {
            SqlParseExample(logger);
        }
        else
        {
            await ConnectionExampleAsync(token, logger);
        }
    }

    private static void SqlParseExample(ILogger logger)
    {
        TSqlParser parser = new TSql150Parser(initialQuotedIdentifiers: true);

        TSqlFragment fragment;
        IList<ParseError> errors;

        var watch = Stopwatch.StartNew();
        using(TextReader reader = new StringReader(SqlConstant.LargeSql))
        {
            fragment = parser.Parse(reader, out errors);
        }

        logger.LogInformation("Parse took: {watch.Elapsed}", watch.Elapsed);
        watch.Restart();

        if (errors.Any())
        {
            foreach (var error in errors)
            {
                logger.LogError("{error.Line}:{error.Offset} {error.Number} {error.Message}", error.Line, error.Offset, error.Number, error.Message);
            }
            return;
        }

        foreach (var asNoOpt in new[]{true, false})
        {
            logger.LogInformation("-- Starting --- asNoOpt:{asNoOpt}", asNoOpt);
            var start = DateTime.UtcNow;
            var factory = WrappedSqlVisitor.CreateFactory(asNoOpt, useRestricted: false, logger);
            logger.LogInformation("[{asNoOpt}] Factory Created {elapsed}", asNoOpt, DateTime.UtcNow - start);
            start = DateTime.UtcNow;
            var restrictedFactory = WrappedSqlVisitor.CreateFactory(asNoOpt, useRestricted: true, logger);
            logger.LogInformation("[{asNoOpt}] Factory Created Restricted {elapsed}", asNoOpt, DateTime.UtcNow - start);

            for (int i = 0; i < 2; i++)
            {
                logger.LogInformation("-- Starting --- {i}", i);
                Parse(NoOptVisitor.Create, asNoOpt, fragment, logger);
                Parse(MoqSqlVisitor.Create, asNoOpt, fragment, logger);
                Parse((x, l) => factory(new SqlParseSidecar()), asNoOpt, fragment, logger);
                Parse((x, l) => restrictedFactory(new SqlParseSidecar()), asNoOpt, fragment, logger);
                logger.LogInformation("-- Ending --- {i}", i);
            }
            logger.LogInformation("-- Ending --- asNoOpt:{asNoOpt}", asNoOpt);
        }

        static void Parse(Func<bool, ILogger, TSqlFragmentVisitor> create, bool asNoOpt, TSqlFragment fragment, ILogger logger, [CallerArgumentExpression("create")] string text = "")
        {
            var start = DateTime.UtcNow;
            logger.LogInformation("{text}[{asNoOpt}] Starting", text, asNoOpt);
            var visitor = create(asNoOpt, logger);
            logger.LogInformation("{text}[{asNoOpt}] Created {elapsed}", text, asNoOpt, DateTime.UtcNow - start);
            var visit = DateTime.UtcNow;
            visitor.Visit(fragment);
            logger.LogInformation("{text}[{asNoOpt}] Visiting {elapsed}", text, asNoOpt, DateTime.UtcNow - visit);
            logger.LogInformation("{text}[{asNoOpt}] Ending {elapsed}", text, asNoOpt, DateTime.UtcNow - start);
        }
    }

    private static async Task ConnectionExampleAsync(string token, ILogger logger)
    {
        Connection connection = new(new ProductHeaderValue("testy-mctest-test"));
        var wrap = ConnectionWrapper.Create(connection, logger);

        var gitHubClient = new GitHubClient(wrap)
        {
            Credentials = new Credentials(token),
        };

        var user = await gitHubClient.User.Get("boxofyellow");
        logger.LogInformation("{user.Id}", user.Id);
        logger.LogInformation("{user.AvatarUrl}", user.AvatarUrl);
    }
}