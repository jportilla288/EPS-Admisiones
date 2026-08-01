/* ===========================================================================
   Modelo de datos relacional del modulo de admisiones (SQL Server 2022).

   Este script es el equivalente exacto de las migraciones de EF Core y se
   incluye por el requisito de entrega. En la practica basta con:
       dotnet ef database update --project src/EPS.Admisiones.Infrastructure ^
                                 --startup-project src/EPS.Admisiones.Web
   =========================================================================== */

IF DB_ID('EpsAdmisiones') IS NULL
BEGIN
    CREATE DATABASE EpsAdmisiones;
END;
GO

USE EpsAdmisiones;
GO

IF SCHEMA_ID('admisiones') IS NULL
BEGIN
    EXEC('CREATE SCHEMA admisiones');
END;
GO

/* ---------------------------------------------------------------------------
   Pacientes (afiliados)
   --------------------------------------------------------------------------- */
IF OBJECT_ID('admisiones.Pacientes') IS NULL
BEGIN
    CREATE TABLE admisiones.Pacientes
    (
        Id              UNIQUEIDENTIFIER NOT NULL,
        TipoDocumento   INT              NOT NULL,   -- 1=CC 2=CE 3=TI 4=RC 5=PA
        NumeroDocumento NVARCHAR(20)     NOT NULL,
        Nombre          NVARCHAR(100)    NOT NULL,
        Apellido        NVARCHAR(100)    NOT NULL,
        Estado          INT              NOT NULL,   -- 1=Activo 2=Inactivo 3=Retirado
        CONSTRAINT PK_Pacientes PRIMARY KEY CLUSTERED (Id)
    );

    -- Unicidad del afiliado garantizada por la BASE DE DATOS: dos instancias
    -- del App Service pueden admitir al mismo paciente en paralelo.
    CREATE UNIQUE INDEX UX_Pacientes_Documento
        ON admisiones.Pacientes (TipoDocumento, NumeroDocumento);
END;
GO

/* ---------------------------------------------------------------------------
   Atenciones facturables (base del reporte de auditoria - Parte 4)
   --------------------------------------------------------------------------- */
IF OBJECT_ID('admisiones.Atenciones') IS NULL
BEGIN
    CREATE TABLE admisiones.Atenciones
    (
        Id                UNIQUEIDENTIFIER NOT NULL,
        PacienteId        UNIQUEIDENTIFIER NOT NULL,
        AdmisionId        UNIQUEIDENTIFIER NOT NULL,
        Valor             DECIMAL(18, 2)   NOT NULL,
        Moneda            NCHAR(3)         NOT NULL,
        RequiereAuditoria BIT              NOT NULL,
        FechaUtc          DATETIME2(7)     NOT NULL,
        CONSTRAINT PK_Atenciones PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Atenciones_Pacientes FOREIGN KEY (PacienteId)
            REFERENCES admisiones.Pacientes (Id) ON DELETE CASCADE
    );

    -- Indice de cobertura del reporte mensual: resuelve filtro y suma sin
    -- volver a la tabla base.
    CREATE INDEX IX_Atenciones_Auditoria_Fecha
        ON admisiones.Atenciones (RequiereAuditoria, FechaUtc)
        INCLUDE (PacienteId, Valor);

    CREATE UNIQUE INDEX UX_Atenciones_AdmisionId
        ON admisiones.Atenciones (AdmisionId);
END;
GO

/* ---------------------------------------------------------------------------
   Admisiones (registro transaccional que bloquea el copago)
   --------------------------------------------------------------------------- */
IF OBJECT_ID('admisiones.Admisiones') IS NULL
BEGIN
    CREATE TABLE admisiones.Admisiones
    (
        Id                     UNIQUEIDENTIFIER NOT NULL,
        PacienteId             UNIQUEIDENTIFIER NOT NULL,
        TipoDocumento          INT              NOT NULL,
        NumeroDocumento        NVARCHAR(20)     NOT NULL,
        ValorCopago            DECIMAL(18, 2)   NOT NULL,
        MonedaCopago           NCHAR(3)         NOT NULL,
        HistoriaClinicaId      NVARCHAR(64)     NOT NULL,
        FechaAdmisionUtc       DATETIME2(7)     NOT NULL,
        Estado                 INT              NOT NULL,  -- 1=Pendiente 2=Sincronizada 3=Fallida
        IntentosSincronizacion INT              NOT NULL CONSTRAINT DF_Admisiones_Intentos DEFAULT (0),
        SincronizadaEnUtc      DATETIME2(7)     NULL,
        MotivoFallo            NVARCHAR(2000)   NULL,
        RowVersion             ROWVERSION       NOT NULL,
        CONSTRAINT PK_Admisiones PRIMARY KEY CLUSTERED (Id)
    );

    CREATE INDEX IX_Admisiones_FechaAdmisionUtc
        ON admisiones.Admisiones (FechaAdmisionUtc DESC);

    -- Indice FILTRADO: solo indexa lo pendiente de conciliar, que son pocas
    -- filas, en lugar del historico completo.
    CREATE INDEX IX_Admisiones_Estado_Pendientes
        ON admisiones.Admisiones (Estado)
        WHERE Estado <> 2;

    CREATE UNIQUE INDEX UX_Admisiones_HistoriaClinicaId
        ON admisiones.Admisiones (HistoriaClinicaId);
END;
GO

/* ---------------------------------------------------------------------------
   Outbox transaccional
   Se escribe en la MISMA transaccion que Admisiones: ahi esta la garantia de
   consistencia del dual write.
   --------------------------------------------------------------------------- */
IF OBJECT_ID('admisiones.OutboxMessages') IS NULL
BEGIN
    CREATE TABLE admisiones.OutboxMessages
    (
        Id                 UNIQUEIDENTIFIER NOT NULL,
        Tipo               NVARCHAR(200)    NOT NULL,
        Payload            NVARCHAR(MAX)    NOT NULL,  -- historia clinica FHIR completa
        CreadoEnUtc        DATETIME2(7)     NOT NULL,
        ProcesadoEnUtc     DATETIME2(7)     NULL,
        Intentos           INT              NOT NULL CONSTRAINT DF_Outbox_Intentos DEFAULT (0),
        DisponibleDesdeUtc DATETIME2(7)     NULL,
        UltimoError        NVARCHAR(2000)   NULL,
        CONSTRAINT PK_OutboxMessages PRIMARY KEY CLUSTERED (Id)
    );

    -- El despachador barre cada 2 segundos: con este indice filtrado solo
    -- recorre los mensajes pendientes, no el historico.
    CREATE INDEX IX_Outbox_Pendientes
        ON admisiones.OutboxMessages (DisponibleDesdeUtc, CreadoEnUtc)
        WHERE ProcesadoEnUtc IS NULL;
END;
GO

PRINT 'Modelo de datos de admisiones creado correctamente.';
GO
