namespace WrapperEmitter.Example;

public static class SqlConstant
{
    public const string LargeSql = @"
CREATE PROCEDURE prc_SomeSproc
    @id1        INT,
    @id2        BIGINT,
    @inputTable typ_ATableType  READONLY,
    @id3        UNIQUEIDENTIFIER,
    @id4        INT
AS

-- Commit 1
SET XACT_ABORT ON
SET NOCOUNT ON

-- Comment 2
CREATE TABLE #missingInputs (
    Path1  NVARCHAR(400)   COLLATE DATABASE_DEFAULT NOT NULL,
    Path2  NVARCHAR(400)   COLLATE DATABASE_DEFAULT NOT NULL

    PRIMARY KEY CLUSTERED (Path1, Path2)
)

-- Comment 3
DECLARE @date1 DATETIME = GETUTCDATE()
DECLARE @id5   INT
DECLARE @path3 NVARCHAR(MAX)
DECLARE @path4 NVARCHAR(MAX)
DECLARE @enum1 TINYINT
DECLARE @path5 NVARCHAR(255)
DECLARE @id6   INT
DECLARE @id7   INT = 0

-- Comment 4
DECLARE @sysName1 SYSNAME
SELECT  @sysName1 = @@SERVERNAME + '.' + db_name() + '..' + object_name(@@PROCID)

-- Comment 5
IF (@@TRANCOUNT = 0)
BEGIN
    SET @path5 = dbo.func_Func1(800000); RAISERROR(@path5, 16, -1, @sysName1)
    RETURN 800000
END

-- Comment 6
Label1:

-- Comment 7
SELECT  TOP(1)
        @path4 = pt.Column1
FROM    @inputTable pt
WHERE   pt.Column1 <> N''
        AND NOT EXISTS (
            SELECT  *
            FROM    tbl_Table1 WITH (FORCESEEK (Index1(Column2, Column3, Column4, Path1, Path2)))
            WHERE   Column2 = @id1
                    AND Column3 = @id4
                    AND Column4 = @id2
                    AND Path1 = CONVERT(NVARCHAR(400), SUBSTRING('\' + pt.Column1, 2, LEN('\' + pt.Column1) - CHARINDEX('\', REVERSE('\' + pt.Column1) COLLATE Latin1_General_BIN2, 2)))
                    AND Path2 = CONVERT(NVARCHAR(400), RIGHT('\' + pt.Column1, CHARINDEX('\', REVERSE('\' + pt.Column1) COLLATE Latin1_General_BIN2, 2) - 1))
                    AND Column5 = 1
        )
OPTION (OPTIMIZE FOR (@id1 UNKNOWN, @id4 UNKNOWN))

-- Comment 8
IF @path4 IS NOT NULL
BEGIN

    -- Comment 9
    INSERT  #missingInputs (Path1, Path2)
    SELECT  DISTINCT
            SUBSTRING('\' + pt.Column1, 2, LEN('\' + pt.Column1) - CHARINDEX('\', REVERSE('\' + pt.Column1) COLLATE Latin1_General_BIN2, 2)),
            RIGHT('\' + pt.Column1, CHARINDEX('\', REVERSE('\' + pt.Column1) COLLATE Latin1_General_BIN2, 2) - 1)
    FROM    @inputTable pt
    WHERE   pt.Column1 <> N''
            AND NOT EXISTS (
                SELECT  *
                FROM    tbl_Table1 WITH (FORCESEEK (Index1(Column2, Column3, Column4, Path1, Path2)))
                WHERE   Column2 = @id1
                        AND Column3 = @id4
                        AND Column4 = @id2
                        AND Path1 = CONVERT(NVARCHAR(400), SUBSTRING('\' + pt.Column1, 2, LEN('\' + pt.Column1) - CHARINDEX('\', REVERSE('\' + pt.Column1) COLLATE Latin1_General_BIN2, 2)))
                        AND Path2 = CONVERT(NVARCHAR(400), RIGHT('\' + pt.Column1, CHARINDEX('\', REVERSE('\' + pt.Column1) COLLATE Latin1_General_BIN2, 2) - 1))
                        AND Column5 = 1
                )
    OPTION (OPTIMIZE FOR (@id1 UNKNOWN, @id4 UNKNOWN))

    -- Comment 10
    SELECT  @id5 = @@ROWCOUNT

    -- Comment 11
    IF (@id5 <> 0)
    BEGIN
        -- Comment 12
        INSERT  #missingInputs (Path1, Path2)
        SELECT  mp.Path1,
                mp.Path2
        FROM    (
                    SELECT  DISTINCT
                            SUBSTRING('\' + LEFT(zq.Path1 + zq.Path2, si.Column6), 2, LEN('\' + LEFT(zq.Path1 + zq.Path2, si.Column6)) - CHARINDEX('\', REVERSE('\' + LEFT(zq.Path1 + zq.Path2, si.Column6)) COLLATE Latin1_General_BIN2, 2)) AS Path1,
                            RIGHT('\' + LEFT(zq.Path1 + zq.Path2, si.Column6), CHARINDEX('\', REVERSE('\' + LEFT(zq.Path1 + zq.Path2, si.Column6)) COLLATE Latin1_General_BIN2, 2) - 1) AS Path2
                    FROM    #missingInputs zq
                    INNER LOOP JOIN tbl_Table2 si
                    ON      SUBSTRING(zq.Path1 + zq.Path2, si.Column6, 1) = '\'
                            AND si.Column6 < LEN(zq.Path1 + zq.Path2)
                            AND LEFT(zq.Path1 + zq.Path2, si.Column6) <> N''
                ) AS mp
            LEFT JOIN #missingInputs yh
            ON      yh.Path1 = mp.Path1
                    AND yh.Path2 = mp.Path2
            WHERE   yh.Path1 IS NULL
                    AND NOT EXISTS (
                        SELECT  *
                        FROM    tbl_Table1 WITH (FORCESEEK (Index1(Column2, Column3, Column4, Path1, Path2)))
                        WHERE   Column2 = @id1
                                AND Column3 = @id4
                                AND Column4 = @id2
                                AND Path1 = mp.Path1
                                AND Path2 = mp.Path2
                                AND Column5 = 1)
        OPTION (OPTIMIZE FOR (@id1 UNKNOWN, @id4 UNKNOWN))

        -- Comment 13
        SET XACT_ABORT OFF

        -- Comment 14
        BEGIN TRY
            INSERT  tbl_Table1 (Column2, Column4, Path1, Path2, Column5, Status,
                                       Column7, Column8, Column9, Column10, Column3)
            SELECT  @id1,
                    @id2,
                    mp.Path1,
                    mp.Path2,
                    1,
                    1,
                    @date1,
                    @date1,
                    @id3,
                    @id3,
                    @id4
            FROM    #missingInputs mp
            OPTION (OPTIMIZE FOR (@id1 UNKNOWN, @id4 UNKNOWN))
        END TRY
        BEGIN CATCH
            SET @id6 = ERROR_NUMBER()

            IF (@id6 = 2601)
            BEGIN

                SELECT  TOP 1
                        @path3 = ci.Path1 + ci.Path2,
                        @enum1 = ci.Column5
                FROM    #missingInputs mp
                JOIN    tbl_Table1 ci
                ON      ci.Column2 = @id1
                        AND ci.Column3 = @id4
                        AND ci.Column4 = @id2
                        AND ci.Path1 = mp.Path1
                        AND ci.Path2 = mp.Path2
                        AND ci.Column5 <> 1
                OPTION (OPTIMIZE FOR (@id1 UNKNOWN, @id4 UNKNOWN))

                IF (@enum1 IS NULL)
                BEGIN
                    -- Comment 15
                    SET @id7 = @id7 + 1

                    IF (@id7 > 10)
                    BEGIN
                        SET @path5 = dbo.func_Func1(800087); RAISERROR(@path5, 16, -1, @sysName1, @path3, @enum1)
                        RETURN 800087
                    END

                    DELETE FROM #missingInputs
                    SET @path4 = NULL

                    SET XACT_ABORT ON
                    GOTO Label1;
                END
                ELSE
                BEGIN
                    -- Comment 16
                    SET @path5 = dbo.func_Func1(800087); RAISERROR(@path5, 16, -1, @sysName1, @path3, @enum1)
                    RETURN 800087
                END
            END
            ELSE
            BEGIN
                SET @path5 = dbo.func_Func1(50000); RAISERROR(@path5, 16, -1, @sysName1, @id6, N'INSERT', N'tbl_Table1')
                RETURN 50000
            END
        END CATCH

        SET XACT_ABORT ON
    END
END
RETURN 0
";
}