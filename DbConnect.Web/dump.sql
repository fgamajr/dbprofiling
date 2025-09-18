PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS "Users" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,
    "Username" TEXT NOT NULL,
    "PasswordHash" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL
);
INSERT INTO Users VALUES(1,'fgamajr','$2a$11$aui/MgKUt7VOa29.4CXU3uwgv9fHtluTAzdwgcluZ8p7VXOXsTrsS','2025-09-11 19:05:42.9661804');
INSERT INTO Users VALUES(2,'teste','$2a$11$XgI3eviHRlr8P5ysvYDUf.cUcXEYgh29PCC.jZ7cgpuH6B0xrSG6O','2025-09-11 20:33:39.3364105');
INSERT INTO Users VALUES(3,'testuser','$2a$11$5uwo4QlF4bT28.L9m19t.u0aV9UJhfabiBRWXtlCAcu.jzu1cJehK','2025-09-11 20:44:04.9987049');
INSERT INTO Users VALUES(4,'validuser','$2a$11$5zI4ApxRynb4aDj42LdPx.MaprAROeIscysP9hsBoRdT1rLiCtWHC','2025-09-11 20:48:53.2503555');
INSERT INTO Users VALUES(5,'admin','$2a$11$8V8dv6q15amLnnyi/ZKMLuYHvpytV9U8WkpGYJ284KJ7PHFQu0VbO','2025-09-11 21:08:32.5198843');
CREATE TABLE IF NOT EXISTS "Profiles" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Profiles" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Kind" INTEGER NOT NULL,
    "HostOrFile" TEXT NOT NULL,
    "Port" INTEGER NULL,
    "Database" TEXT NOT NULL,
    "Username" TEXT NOT NULL,
    "Password" TEXT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UserId" INTEGER NOT NULL,
    CONSTRAINT "FK_Profiles_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);
INSERT INTO Profiles VALUES(3,'caf_mapa',0,'172.23.80.1',5433,'caf_mapa','postgres','Maizena90','2025-09-11 21:10:48.970549',1);
CREATE TABLE IF NOT EXISTS "Reports" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Reports" PRIMARY KEY AUTOINCREMENT,
    "UserId" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "Kind" TEXT NOT NULL,
    "InputSignature" TEXT NOT NULL,
    "StoragePath" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    CONSTRAINT "FK_Reports_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);
INSERT INTO __EFMigrationsHistory VALUES('20250913021857_AddUserApiSettings','8.0.4');
INSERT INTO __EFMigrationsHistory VALUES('20250914020959_AddDataQualityTables','8.0.4');
INSERT INTO __EFMigrationsHistory VALUES('20250914132151_AddCustomDataQualityRules','8.0.4');
INSERT INTO __EFMigrationsHistory VALUES('20250914133358_AddRuleVersioning','8.0.4');
INSERT INTO __EFMigrationsHistory VALUES('20250914233940_FixRuleExecutionLongFields','8.0.4');
INSERT INTO __EFMigrationsHistory VALUES('20250915101838_AddTableEssentialMetrics','8.0.4');
CREATE TABLE IF NOT EXISTS "UserApiSettings" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_UserApiSettings" PRIMARY KEY AUTOINCREMENT,
    "UserId" INTEGER NOT NULL,
    "Provider" TEXT NOT NULL,
    "ApiKeyEncrypted" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "LastValidatedAtUtc" TEXT NULL,
    CONSTRAINT "FK_UserApiSettings_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);
INSERT INTO UserApiSettings VALUES(1,1,'openai','1wYXEX5AD4+rBDmcZgowMDsFp4AIRLJmOZ9+wpJLucdPVbef83Ko/kRcZkFrt/diY+UqqFnGNZ9avPpLlP+c+MPGn5pTTW7uFIdHBwtxgFhbTlJNkNa1o8SADWS/wJtVIZNyAvLyGoEV5UZqOm1daYxbLgGNX2w75UTtYmVtIGlcjiHhcXe4bdcwLvaGjQNrgAKBqECEO1xEC4byqPFmewQtzV4RWpy+pO/jDLdpxDOatu4NxjqjQMlo9kXp/tj6',1,'2025-09-14 00:45:17.5569101','2025-09-14 00:45:17.5571437');
CREATE TABLE IF NOT EXISTS "DataQualityAnalyses" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_DataQualityAnalyses" PRIMARY KEY AUTOINCREMENT,
    "UserId" INTEGER NOT NULL,
    "ProfileId" INTEGER NOT NULL,
    "TableName" TEXT NOT NULL,
    "Schema" TEXT NOT NULL,
    "Provider" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "CompletedAtUtc" TEXT NULL,
    "Status" TEXT NOT NULL,
    "ErrorMessage" TEXT NULL,
    CONSTRAINT "FK_DataQualityAnalyses_Profiles_ProfileId" FOREIGN KEY ("ProfileId") REFERENCES "Profiles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_DataQualityAnalyses_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);
INSERT INTO DataQualityAnalyses VALUES(1,1,3,'S_RENDA','caf_mapa','OPENAI','2025-09-14 23:26:36.0740878','2025-09-14 23:26:39.7807222','completed',NULL);
INSERT INTO DataQualityAnalyses VALUES(2,1,3,'S_RENDA','caf_mapa','OPENAI','2025-09-14 23:33:51.9089838','2025-09-14 23:33:55.2377746','completed',NULL);
INSERT INTO DataQualityAnalyses VALUES(3,1,3,'S_DOCUMENTO','caf_mapa','OPENAI','2025-09-14 23:37:24.4336523','2025-09-14 23:37:32.9858639','completed',NULL);
INSERT INTO DataQualityAnalyses VALUES(4,1,3,'S_RENDA','caf_mapa','OPENAI','2025-09-14 23:43:59.4246863','2025-09-14 23:44:03.0697924','completed',NULL);
INSERT INTO DataQualityAnalyses VALUES(5,1,3,'S_DOC_IDENTIFICACAO','caf_mapa','OPENAI','2025-09-17 11:49:23.0260739','2025-09-17 11:49:24.9106779','completed',NULL);
INSERT INTO DataQualityAnalyses VALUES(6,1,3,'S_DOCUMENTO','caf_mapa','OPENAI','2025-09-17 11:58:31.3766052','2025-09-17 11:58:45.6793467','completed',NULL);
INSERT INTO DataQualityAnalyses VALUES(7,1,3,'S_DOCUMENTO','caf_mapa','OPENAI','2025-09-17 12:10:51.869101','2025-09-17 12:11:05.5766743','completed',NULL);
INSERT INTO DataQualityAnalyses VALUES(8,1,3,'S_DOCUMENTO','caf_mapa','OPENAI','2025-09-17 12:14:45.5778952','2025-09-17 12:15:01.127651','completed',NULL);
INSERT INTO DataQualityAnalyses VALUES(9,1,3,'S_DOCUMENTO','caf_mapa','OPENAI','2025-09-17 12:18:25.7281411','2025-09-17 12:18:38.7538636','completed',NULL);
INSERT INTO DataQualityAnalyses VALUES(10,1,3,'S_DOCUMENTO','caf_mapa','OPENAI','2025-09-17 12:26:29.7291747','2025-09-17 12:26:37.53831','completed',NULL);
INSERT INTO DataQualityAnalyses VALUES(11,1,3,'S_DOCUMENTO','caf_mapa','OPENAI','2025-09-17 12:34:23.1973129','2025-09-17 12:34:35.4525741','completed',NULL);
INSERT INTO DataQualityAnalyses VALUES(12,1,3,'S_DOCUMENTO','caf_mapa','OPENAI','2025-09-17 12:37:00.8425383','2025-09-17 12:37:09.8408492','completed',NULL);
INSERT INTO DataQualityAnalyses VALUES(13,1,3,'S_DOCUMENTO','caf_mapa','OPENAI','2025-09-17 12:48:52.4547686','2025-09-17 12:49:03.9428903','completed',NULL);
CREATE TABLE IF NOT EXISTS "DataQualityResults" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_DataQualityResults" PRIMARY KEY AUTOINCREMENT,
    "AnalysisId" INTEGER NOT NULL,
    "RuleId" TEXT NOT NULL,
    "RuleName" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "Dimension" TEXT NOT NULL,
    "Column" TEXT NOT NULL,
    "SqlCondition" TEXT NOT NULL,
    "Severity" TEXT NOT NULL,
    "ExpectedPassRate" REAL NOT NULL,
    "Status" TEXT NOT NULL,
    "ActualPassRate" REAL NOT NULL,
    "TotalRecords" INTEGER NOT NULL,
    "ValidRecords" INTEGER NOT NULL,
    "InvalidRecords" INTEGER NOT NULL,
    "ErrorMessage" TEXT NULL,
    "ExecutedAtUtc" TEXT NOT NULL,
    CONSTRAINT "FK_DataQualityResults_DataQualityAnalyses_AnalysisId" FOREIGN KEY ("AnalysisId") REFERENCES "DataQualityAnalyses" ("Id") ON DELETE CASCADE
);
INSERT INTO DataQualityResults VALUES(1,1,'rule_001','Verificar id_renda não nulo','A coluna id_renda não deve conter valores nulos.','completeness','id_renda','id_renda IS NOT NULL','error',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:26:36.624332');
INSERT INTO DataQualityResults VALUES(2,1,'rule_002','Verificar vl_renda_auferida não nulo','A coluna vl_renda_auferida não deve conter valores nulos.','completeness','vl_renda_auferida','vl_renda_auferida IS NOT NULL','error',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:26:36.8501375');
INSERT INTO DataQualityResults VALUES(3,1,'rule_003','Verificar id_unidade_familiar não nulo','A coluna id_unidade_familiar não deve conter valores nulos.','completeness','id_unidade_familiar','id_unidade_familiar IS NOT NULL','error',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:26:37.0412433');
INSERT INTO DataQualityResults VALUES(4,1,'rule_004','Verificar unicidade de id_renda','A coluna id_renda deve ser única.','uniqueness','id_renda','id_renda IS NOT NULL','error',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:26:37.2386998');
INSERT INTO DataQualityResults VALUES(5,1,'rule_005','Verificar consistência entre vl_renda_estimada e vl_renda_auferida','A coluna vl_renda_estimada deve ser maior ou igual a vl_renda_auferida quando vl_renda_estimada não for nula.','consistency','vl_renda_estimada','vl_renda_estimada IS NOT NULL AND vl_renda_estimada < vl_renda_auferida','error',95.0,'fail',0.309999999999999997,12849431,40397,12809034,NULL,'2025-09-14 23:26:37.7767299');
INSERT INTO DataQualityResults VALUES(6,1,'rule_006','Verificar se dt_criacao não é futura','A coluna dt_criacao não deve conter datas futuras.','timeliness','dt_criacao','dt_criacao > CURRENT_DATE','error',95.0,'fail',0.0,12849431,0,12849431,NULL,'2025-09-14 23:26:38.2104978');
INSERT INTO DataQualityResults VALUES(7,1,'rule_007','Verificar se dt_atualizacao não é futura','A coluna dt_atualizacao não deve conter datas futuras.','timeliness','dt_atualizacao','dt_atualizacao > CURRENT_DATE','error',95.0,'fail',0.0,12849431,0,12849431,NULL,'2025-09-14 23:26:38.6604866');
INSERT INTO DataQualityResults VALUES(8,1,'rule_008','Verificar se vl_renda_estimada está dentro de um intervalo válido','A coluna vl_renda_estimada deve ser maior ou igual a 0.','accuracy','vl_renda_estimada','vl_renda_estimada < 0','error',95.0,'fail',0.0,12849431,0,12849431,NULL,'2025-09-14 23:26:39.1184842');
INSERT INTO DataQualityResults VALUES(9,1,'rule_009','Verificar se st_producao_agroecologica é booleano','A coluna st_producao_agroecologica deve ser um valor booleano ou nulo.','validity','st_producao_agroecologica','st_producao_agroecologica IS NOT NULL AND st_producao_agroecologica NOT IN (true, false)','error',95.0,'fail',0.0,12849431,0,12849431,NULL,'2025-09-14 23:26:39.5693161');
INSERT INTO DataQualityResults VALUES(10,1,'rule_010','Verificar id_unidade_familiar_pessoa quando id_unidade_familiar não é nulo','A coluna id_unidade_familiar_pessoa pode ser nula apenas se id_unidade_familiar não for nulo.','consistency','id_unidade_familiar_pessoa','id_unidade_familiar IS NOT NULL OR id_unidade_familiar_pessoa IS NULL','warning',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:26:39.7564957');
INSERT INTO DataQualityResults VALUES(11,2,'rule_001','Verificar id_renda não nulo','A coluna id_renda não deve conter valores nulos.','completeness','id_renda','id_renda IS NOT NULL','error',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:33:52.443767');
INSERT INTO DataQualityResults VALUES(12,2,'rule_002','Verificar vl_renda_auferida não nulo','A coluna vl_renda_auferida não deve conter valores nulos.','completeness','vl_renda_auferida','vl_renda_auferida IS NOT NULL','error',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:33:52.6432925');
INSERT INTO DataQualityResults VALUES(13,2,'rule_003','Verificar id_unidade_familiar não nulo','A coluna id_unidade_familiar não deve conter valores nulos.','completeness','id_unidade_familiar','id_unidade_familiar IS NOT NULL','error',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:33:52.8464256');
INSERT INTO DataQualityResults VALUES(14,2,'rule_004','Verificar unicidade de id_renda','A coluna id_renda deve ser única.','uniqueness','id_renda','id_renda IS NOT NULL','error',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:33:53.0614762');
INSERT INTO DataQualityResults VALUES(15,2,'rule_005','Verificar consistência entre vl_renda_estimada e vl_renda_auferida','A coluna vl_renda_estimada deve ser maior ou igual a vl_renda_auferida quando vl_renda_estimada não for nula.','consistency','vl_renda_estimada','vl_renda_estimada IS NOT NULL AND vl_renda_estimada < vl_renda_auferida','error',95.0,'fail',0.309999999999999997,12849431,40397,12809034,NULL,'2025-09-14 23:33:53.6219736');
INSERT INTO DataQualityResults VALUES(16,2,'rule_006','Verificar valores de vl_renda_estimada','A coluna vl_renda_estimada deve ser maior ou igual a 0.','accuracy','vl_renda_estimada','vl_renda_estimada < 0','error',95.0,'fail',0.0,12849431,0,12849431,NULL,'2025-09-14 23:33:54.0960628');
INSERT INTO DataQualityResults VALUES(17,2,'rule_007','Verificar data de criação não futura','A coluna dt_criacao não deve conter datas futuras.','timeliness','dt_criacao','dt_criacao > CURRENT_DATE','error',95.0,'fail',0.0,12849431,0,12849431,NULL,'2025-09-14 23:33:54.5368217');
INSERT INTO DataQualityResults VALUES(18,2,'rule_008','Verificar id_unidade_familiar_pessoa quando id_unidade_familiar não é nulo','A coluna id_unidade_familiar_pessoa pode ser nula, mas se não for, id_unidade_familiar deve ser válido.','consistency','id_unidade_familiar_pessoa','id_unidade_familiar IS NOT NULL OR id_unidade_familiar_pessoa IS NULL','warning',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:33:54.7347992');
INSERT INTO DataQualityResults VALUES(19,2,'rule_009','Verificar id_usuario quando st_producao_agroecologica é verdadeiro','Se st_producao_agroecologica for verdadeiro, id_usuario não pode ser nulo.','consistency','id_usuario','st_producao_agroecologica = true AND id_usuario IS NULL','error',95.0,'fail',0.719999999999999973,12849431,92407,12757024,NULL,'2025-09-14 23:33:55.213114');
INSERT INTO DataQualityResults VALUES(20,3,'rule_001','Verificação de id_documento não nulo','O campo id_documento não pode ser nulo.','completeness','id_documento','id_documento IS NOT NULL','error',95.0,'pass',100.0,11377318,11377318,0,NULL,'2025-09-14 23:37:24.9468367');
INSERT INTO DataQualityResults VALUES(21,3,'rule_002','Verificação de nm_documento não nulo','O campo nm_documento não pode ser nulo ou vazio.','completeness','nm_documento','nm_documento IS NOT NULL AND LENGTH(TRIM(nm_documento::text)) > 0','error',95.0,'pass',100.0,11377318,11377318,0,NULL,'2025-09-14 23:37:26.5672397');
INSERT INTO DataQualityResults VALUES(22,3,'rule_003','Verificação de cd_cloud_storage não nulo','O campo cd_cloud_storage não pode ser nulo.','completeness','cd_cloud_storage','cd_cloud_storage IS NOT NULL','error',95.0,'pass',100.0,11377318,11377318,0,NULL,'2025-09-14 23:37:26.736753');
INSERT INTO DataQualityResults VALUES(23,3,'rule_004','Verificação de id_tipo_documento não nulo','O campo id_tipo_documento não pode ser nulo.','completeness','id_tipo_documento','id_tipo_documento IS NOT NULL','error',95.0,'pass',100.0,11377318,11377318,0,NULL,'2025-09-14 23:37:26.8992034');
INSERT INTO DataQualityResults VALUES(24,3,'rule_005','Verificação de id_unidade_familiar único','O campo id_unidade_familiar deve ser único quando não nulo.','uniqueness','id_unidade_familiar','id_unidade_familiar IS NOT NULL','warning',95.0,'pass',99.8400000000000034,11377318,11359199,18119,NULL,'2025-09-14 23:37:28.4707604');
INSERT INTO DataQualityResults VALUES(25,3,'rule_006','Verificação de dt_criacao não futura','A data de criação não pode ser uma data futura.','timeliness','dt_criacao','dt_criacao <= CURRENT_DATE','error',95.0,'pass',100.0,11377318,11377318,0,NULL,'2025-09-14 23:37:29.3057504');
INSERT INTO DataQualityResults VALUES(26,3,'rule_007','Verificação de dt_atualizacao não futura','A data de atualização não pode ser uma data futura.','timeliness','dt_atualizacao','dt_atualizacao IS NULL OR dt_atualizacao <= CURRENT_DATE','error',95.0,'pass',100.0,11377318,11377318,0,NULL,'2025-09-14 23:37:30.0118655');
INSERT INTO DataQualityResults VALUES(27,3,'rule_008','Verificação de st_migrado_nuvem booleano','O campo st_migrado_nuvem deve ser um valor booleano.','validity','st_migrado_nuvem','st_migrado_nuvem IS NOT NULL','error',95.0,'pass',100.0,11377318,11377318,0,NULL,'2025-09-14 23:37:30.2254037');
INSERT INTO DataQualityResults VALUES(28,3,'rule_009','Verificação de nm_extensao válida','O campo nm_extensao deve ser um valor válido quando não nulo.','validity','nm_extensao','nm_extensao IS NULL OR nm_extensao ~* ''^(pdf|jpg|jpeg|png)$''','warning',95.0,'pass',99.9200000000000017,11377318,11367754,9564,NULL,'2025-09-14 23:37:32.2621675');
INSERT INTO DataQualityResults VALUES(29,3,'rule_010','Verificação de id_pessoa_juridica e id_unidade_familiar','Se id_pessoa_juridica for nulo, id_unidade_familiar deve ser obrigatório.','consistency','id_pessoa_juridica','id_pessoa_juridica IS NULL OR id_unidade_familiar IS NOT NULL','error',95.0,'pass',99.8400000000000034,11377318,11359224,18094,NULL,'2025-09-14 23:37:32.9609579');
INSERT INTO DataQualityResults VALUES(30,4,'rule_001','Verificação de ID Renda','O campo id_renda não deve ser nulo.','completeness','id_renda','id_renda IS NOT NULL','error',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:43:59.9424398');
INSERT INTO DataQualityResults VALUES(31,4,'rule_002','Verificação de Valor de Renda Auferida','O campo vl_renda_auferida não deve ser nulo.','completeness','vl_renda_auferida','vl_renda_auferida IS NOT NULL','error',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:44:00.1402953');
INSERT INTO DataQualityResults VALUES(32,4,'rule_003','Verificação de ID Unidade Familiar','O campo id_unidade_familiar não deve ser nulo.','completeness','id_unidade_familiar','id_unidade_familiar IS NOT NULL','error',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:44:00.3365511');
INSERT INTO DataQualityResults VALUES(33,4,'rule_004','Verificação de Unicidade de ID Renda','O campo id_renda deve ser único.','uniqueness','id_renda','id_renda IS NOT NULL','error',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:44:00.5362801');
INSERT INTO DataQualityResults VALUES(34,4,'rule_005','Verificação de Consistência de Renda Estimada e Auferida','O valor de vl_renda_estimada deve ser menor ou igual ao valor de vl_renda_auferida.','consistency','vl_renda_estimada','vl_renda_estimada <= vl_renda_auferida OR vl_renda_estimada IS NULL','warning',95.0,'pass',96.7399999999999948,12849431,12431158,418273,NULL,'2025-09-14 23:44:01.1493252');
INSERT INTO DataQualityResults VALUES(35,4,'rule_006','Verificação de Data de Criação','A data de criação não deve ser futura.','timeliness','dt_criacao','dt_criacao <= CURRENT_DATE','error',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:44:01.6319807');
INSERT INTO DataQualityResults VALUES(36,4,'rule_007','Verificação de Data de Atualização','A data de atualização não deve ser futura.','timeliness','dt_atualizacao','dt_atualizacao <= CURRENT_DATE OR dt_atualizacao IS NULL','warning',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:44:02.1430303');
INSERT INTO DataQualityResults VALUES(37,4,'rule_008','Verificação de ID Tipo Renda','O campo id_tipo_renda não deve ser nulo.','completeness','id_tipo_renda','id_tipo_renda IS NOT NULL','error',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:44:02.3251202');
INSERT INTO DataQualityResults VALUES(38,4,'rule_009','Verificação de ID Produto','O campo id_produto não deve ser nulo.','completeness','id_produto','id_produto IS NOT NULL','error',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:44:02.5133385');
INSERT INTO DataQualityResults VALUES(39,4,'rule_010','Verificação de Valor de Renda Estimada','O valor de vl_renda_estimada deve ser maior ou igual a zero.','accuracy','vl_renda_estimada','vl_renda_estimada >= 0 OR vl_renda_estimada IS NULL','warning',95.0,'pass',100.0,12849431,12849431,0,NULL,'2025-09-14 23:44:03.0455395');
INSERT INTO DataQualityResults VALUES(40,5,'rule_001','Verificação de ID de Documento','O campo id_doc_identificacao não pode ser nulo.','completeness','id_doc_identificacao','id_doc_identificacao IS NULL','error',95.0,'fail',0.0,4146659,0,4146659,NULL,'2025-09-17 11:49:23.2651643');
INSERT INTO DataQualityResults VALUES(41,5,'rule_002','Verificação de UF','O campo sg_uf não pode ser nulo e deve ter exatamente 2 caracteres.','completeness','sg_uf','sg_uf IS NULL OR LENGTH(TRIM(sg_uf::text)) != 2','error',95.0,'fail',0.0,4146659,0,4146659,NULL,'2025-09-17 11:49:23.5332587');
INSERT INTO DataQualityResults VALUES(42,5,'rule_003','Verificação de Número do Documento','O campo nr_documento não pode ser nulo e deve ser numérico.','completeness','nr_documento','nr_documento IS NULL OR nr_documento !~ ''^[0-9]+$''','error',95.0,'fail',3.18999999999999994,4146659,132087,4014572,NULL,'2025-09-17 11:49:23.951149');
INSERT INTO DataQualityResults VALUES(43,5,'rule_004','Verificação de ID de Pessoa Física','O campo id_pessoa_fisica não pode ser nulo.','completeness','id_pessoa_fisica','id_pessoa_fisica IS NULL','error',95.0,'fail',0.0,4146659,0,4146659,NULL,'2025-09-17 11:49:23.9523906');
INSERT INTO DataQualityResults VALUES(44,5,'rule_005','Verificação de Unicidade do Número do Documento','O campo nr_documento deve ser único.','uniqueness','nr_documento','nr_documento IS NOT NULL AND (SELECT COUNT(*) FROM caf_mapa.S_DOC_IDENTIFICACAO WHERE nr_documento = S_DOC_IDENTIFICACAO.nr_documento) > 1','error',95.0,'error',0.0,4146659,0,4146659,replace('42P01: relação "caf_mapa.s_doc_identificacao" não existe\n\nPOSITION: 112','\n',char(10)),'2025-09-17 11:49:23.9587009');
INSERT INTO DataQualityResults VALUES(45,5,'rule_006','Verificação de Data de Emissão','A data de emissão não pode ser futura.','timeliness','dt_emissao','dt_emissao IS NOT NULL AND dt_emissao > CURRENT_DATE','error',95.0,'fail',0.0,4146659,0,4146659,NULL,'2025-09-17 11:49:24.0877438');
INSERT INTO DataQualityResults VALUES(46,5,'rule_007','Verificação de Consistência do Emissor','O campo nm_emissor_orgao deve ser preenchido se o id_tipo_documento for 1.','consistency','nm_emissor_orgao','id_tipo_documento = 1 AND nm_emissor_orgao IS NULL','error',95.0,'fail',0.0,4146659,0,4146659,NULL,'2025-09-17 11:49:24.2332702');
INSERT INTO DataQualityResults VALUES(47,5,'rule_008','Verificação de Comprimento do Número do Documento','O campo nr_documento deve ter entre 1 e 15 caracteres.','accuracy','nr_documento','LENGTH(TRIM(nr_documento::text)) < 1 OR LENGTH(TRIM(nr_documento::text)) > 15','warning',95.0,'fail',0.0299999999999999988,4146659,1410,4145249,NULL,'2025-09-17 11:49:24.615146');
INSERT INTO DataQualityResults VALUES(48,5,'rule_009','Verificação de Consistência do Tipo de Documento','O campo id_tipo_documento deve ser um número positivo.','accuracy','id_tipo_documento','id_tipo_documento <= 0','error',95.0,'fail',0.0,4146659,0,4146659,NULL,'2025-09-17 11:49:24.6164466');
INSERT INTO DataQualityResults VALUES(49,5,'rule_010','Verificação de Emissor Válido','O campo nm_emissor_orgao deve ter pelo menos 3 caracteres se preenchido.','validity','nm_emissor_orgao','nm_emissor_orgao IS NOT NULL AND LENGTH(TRIM(nm_emissor_orgao::text)) < 3','warning',95.0,'fail',3.97999999999999998,4146659,164891,3981768,NULL,'2025-09-17 11:49:24.880959');
INSERT INTO DataQualityResults VALUES(50,6,'rule_001','Verificação de Nulos em Campos Obrigatórios','Verifica se os campos obrigatórios não estão nulos ou vazios.','completeness','id_documento','id_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 11:58:31.9300665');
INSERT INTO DataQualityResults VALUES(51,6,'rule_002','Verificação de Nulos em Campos Obrigatórios','Verifica se o nome do documento não está nulo ou vazio.','completeness','nm_documento','nm_documento IS NOT NULL AND LENGTH(TRIM(nm_documento::text)) > 0','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 11:58:33.9015644');
INSERT INTO DataQualityResults VALUES(52,6,'rule_003','Verificação de Nulos em Campos Obrigatórios','Verifica se o código de armazenamento em nuvem não está nulo ou vazio.','completeness','cd_cloud_storage','cd_cloud_storage IS NOT NULL AND LENGTH(TRIM(cd_cloud_storage::text)) > 0','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 11:58:35.568097');
INSERT INTO DataQualityResults VALUES(53,6,'rule_004','Verificação de Unicidade de ID do Documento','Verifica se o ID do documento é único.','uniqueness','id_documento','id_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 11:58:35.7824869');
INSERT INTO DataQualityResults VALUES(54,6,'rule_005','Verificação de Formato de URL','Verifica se a URL do documento está em um formato válido.','validity','ds_url','ds_url ~* ''^https?://.*$''','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 11:58:41.5708162');
INSERT INTO DataQualityResults VALUES(55,6,'rule_006','Verificação de Data de Criação','Verifica se a data de criação não é futura.','timeliness','dt_criacao','dt_criacao <= CURRENT_DATE','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 11:58:42.5242624');
INSERT INTO DataQualityResults VALUES(56,6,'rule_007','Verificação de Consistência entre ID de Unidade Familiar e ID de Pessoa Jurídica','Verifica se o ID da unidade familiar é nulo quando o ID da pessoa jurídica não é nulo.','consistency','id_unidade_familiar','id_unidade_familiar IS NULL OR id_pessoa_juridica IS NOT NULL','warning',95.0,'pass',99.8400000000000034,11377318,11359199,18119,NULL,'2025-09-17 11:58:42.6117878');
INSERT INTO DataQualityResults VALUES(57,6,'rule_008','Verificação de Extensão de Arquivo','Verifica se a extensão do arquivo é válida.','validity','nm_extensao','nm_extensao ~* ''^(pdf|PDF)$''','warning',95.0,'fail',5.04000000000000003,11377318,573612,10803706,NULL,'2025-09-17 11:58:44.4889967');
INSERT INTO DataQualityResults VALUES(58,6,'rule_009','Verificação de Data de Atualização','Verifica se a data de atualização não é futura.','timeliness','dt_atualizacao','dt_atualizacao IS NULL OR dt_atualizacao <= CURRENT_DATE','warning',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 11:58:45.3727993');
INSERT INTO DataQualityResults VALUES(59,6,'rule_010','Verificação de Migrado para Nuvem','Verifica se o status de migração para nuvem é verdadeiro.','completeness','st_migrado_nuvem','st_migrado_nuvem IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 11:58:45.649284');
INSERT INTO DataQualityResults VALUES(60,7,'rule_001','Verificação de Nulos em Campos Obrigatórios','Verifica se os campos obrigatórios não estão nulos.','completeness','id_documento','id_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:10:52.4137873');
INSERT INTO DataQualityResults VALUES(61,7,'rule_002','Verificação de Nulos em Campos Obrigatórios','Verifica se o nome do documento não está nulo.','completeness','nm_documento','nm_documento IS NOT NULL AND TRIM(nm_documento::text) != ''''','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:10:53.6641896');
INSERT INTO DataQualityResults VALUES(62,7,'rule_003','Verificação de Nulos em Campos Obrigatórios','Verifica se o código de armazenamento em nuvem não está nulo.','completeness','cd_cloud_storage','cd_cloud_storage IS NOT NULL AND TRIM(cd_cloud_storage::text) != ''''','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:10:54.9183271');
INSERT INTO DataQualityResults VALUES(63,7,'rule_004','Verificação de Unicidade de ID do Documento','Verifica se o ID do documento é único.','uniqueness','id_documento','id_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:10:55.1176746');
INSERT INTO DataQualityResults VALUES(64,7,'rule_005','Verificação de Formato da URL','Verifica se a URL está em um formato válido.','validity','ds_url','ds_url ~* ''^https?://.*$''','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:11:00.8742694');
INSERT INTO DataQualityResults VALUES(65,7,'rule_006','Verificação de Data de Criação','Verifica se a data de criação não é futura.','timeliness','dt_criacao','dt_criacao <= CURRENT_DATE','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:11:01.8176222');
INSERT INTO DataQualityResults VALUES(66,7,'rule_007','Verificação de Consistência entre ID da Unidade Familiar e ID do Documento','Verifica se o ID da unidade familiar é nulo apenas se o ID do documento não for nulo.','consistency','id_unidade_familiar','id_unidade_familiar IS NOT NULL OR id_documento IS NULL','warning',95.0,'fail',0.160000000000000003,11377318,18119,11359199,NULL,'2025-09-17 12:11:02.6580043');
INSERT INTO DataQualityResults VALUES(67,7,'rule_008','Verificação de Extensão do Documento','Verifica se a extensão do documento é válida.','validity','nm_extensao','nm_extensao ~* ''^(pdf|PDF)$''','warning',95.0,'fail',5.04000000000000003,11377318,573612,10803706,NULL,'2025-09-17 12:11:04.4779824');
INSERT INTO DataQualityResults VALUES(68,7,'rule_009','Verificação de Data de Atualização','Verifica se a data de atualização não é futura.','timeliness','dt_atualizacao','dt_atualizacao IS NULL OR dt_atualizacao <= CURRENT_DATE','warning',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:11:05.3513406');
INSERT INTO DataQualityResults VALUES(69,7,'rule_010','Verificação de Migração para Nuvem','Verifica se o status de migração para nuvem é verdadeiro.','completeness','st_migrado_nuvem','st_migrado_nuvem IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:11:05.5476884');
INSERT INTO DataQualityResults VALUES(70,8,'rule_001','Verificação de Nulos em Campos Obrigatórios','Verifica se os campos obrigatórios não estão nulos.','completeness','id_documento','id_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:14:46.1882079');
INSERT INTO DataQualityResults VALUES(71,8,'rule_002','Verificação de Nulos em Campos Obrigatórios','Verifica se os campos obrigatórios não estão nulos.','completeness','nm_documento','nm_documento IS NOT NULL AND TRIM(nm_documento::text) != ''''','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:14:47.5638295');
INSERT INTO DataQualityResults VALUES(72,8,'rule_003','Verificação de Nulos em Campos Obrigatórios','Verifica se os campos obrigatórios não estão nulos.','completeness','cd_cloud_storage','cd_cloud_storage IS NOT NULL AND TRIM(cd_cloud_storage::text) != ''''','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:14:48.8557238');
INSERT INTO DataQualityResults VALUES(73,8,'rule_004','Verificação de Nulos em Campos Obrigatórios','Verifica se os campos obrigatórios não estão nulos.','completeness','ds_url','ds_url IS NOT NULL AND TRIM(ds_url::text) != ''''','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:14:50.656326');
INSERT INTO DataQualityResults VALUES(74,8,'rule_005','Verificação de Unicidade de ID do Documento','Verifica se o ID do documento é único.','uniqueness','id_documento','id_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:14:50.8694772');
INSERT INTO DataQualityResults VALUES(75,8,'rule_006','Validação de Formato da URL','Verifica se a URL está em um formato válido.','validity','ds_url','ds_url ~* ''^https?://.*$''','warning',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:14:56.6138372');
INSERT INTO DataQualityResults VALUES(76,8,'rule_007','Verificação de Data de Criação','Verifica se a data de criação não é futura.','timeliness','dt_criacao','dt_criacao <= CURRENT_DATE','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:14:57.506434');
INSERT INTO DataQualityResults VALUES(77,8,'rule_008','Verificação de Consistência entre ID da Unidade Familiar e ID do Documento','Verifica se o ID da unidade familiar é nulo apenas se o ID do documento não for nulo.','consistency','id_unidade_familiar','id_unidade_familiar IS NOT NULL OR id_documento IS NULL','warning',95.0,'fail',0.160000000000000003,11377318,18119,11359199,NULL,'2025-09-17 12:14:58.380845');
INSERT INTO DataQualityResults VALUES(78,8,'rule_009','Verificação de Extensão do Documento','Verifica se a extensão do documento é válida.','validity','nm_extensao','nm_extensao ~* ''^(pdf|PDF)$''','warning',95.0,'fail',5.04000000000000003,11377318,573612,10803706,NULL,'2025-09-17 12:15:00.1781121');
INSERT INTO DataQualityResults VALUES(79,8,'rule_010','Verificação de Nulos em Campos de Atualização','Verifica se a data de atualização é nula quando o documento não foi atualizado.','consistency','dt_atualizacao','dt_atualizacao IS NULL OR st_migrado_nuvem = true','info',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:15:01.0992063');
INSERT INTO DataQualityResults VALUES(80,9,'rule_001','Verificação de ID do Documento','O campo id_documento não pode ser nulo.','completeness','id_documento','id_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:18:26.1561436');
INSERT INTO DataQualityResults VALUES(81,9,'rule_002','Verificação de Nome do Documento','O campo nm_documento não pode ser nulo ou vazio.','completeness','nm_documento','LENGTH(TRIM(nm_documento::text)) > 0','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:18:27.6039397');
INSERT INTO DataQualityResults VALUES(82,9,'rule_003','Verificação de CD Cloud Storage','O campo cd_cloud_storage não pode ser nulo ou vazio.','completeness','cd_cloud_storage','LENGTH(TRIM(cd_cloud_storage::text)) > 0','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:18:29.1761009');
INSERT INTO DataQualityResults VALUES(83,9,'rule_004','Verificação de URL','O campo ds_url não pode ser nulo ou vazio.','completeness','ds_url','LENGTH(TRIM(ds_url::text)) > 0','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:18:31.9944631');
INSERT INTO DataQualityResults VALUES(84,9,'rule_005','Verificação de Unicidade do ID do Documento','O campo id_documento deve ser único.','uniqueness','id_documento','id_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:18:32.2030352');
INSERT INTO DataQualityResults VALUES(85,9,'rule_006','Verificação de Tipo de Documento','O campo id_tipo_documento deve ser um número inteiro positivo.','validity','id_tipo_documento','id_tipo_documento > 0','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:18:33.9316738');
INSERT INTO DataQualityResults VALUES(86,9,'rule_007','Verificação de Data de Criação','A data de criação não pode ser futura.','timeliness','dt_criacao','dt_criacao <= CURRENT_DATE','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:18:34.94525');
INSERT INTO DataQualityResults VALUES(87,9,'rule_008','Verificação de Data de Atualização','A data de atualização, se presente, não pode ser futura e deve ser maior ou igual à data de criação.','timeliness','dt_atualizacao','dt_atualizacao IS NULL OR (dt_atualizacao <= CURRENT_DATE AND dt_atualizacao >= dt_criacao)','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:18:35.9521851');
INSERT INTO DataQualityResults VALUES(88,9,'rule_009','Verificação de Extensão do Documento','A extensão do documento deve ser válida (pdf, PDF).','validity','nm_extensao','nm_extensao IS NULL OR nm_extensao ~* ''^(pdf|PDF)$''','warning',95.0,'fail',5.03000000000000024,11377318,572620,10804698,NULL,'2025-09-17 12:18:37.8991592');
INSERT INTO DataQualityResults VALUES(89,9,'rule_010','Verificação de Unidade Familiar','O campo id_unidade_familiar deve ser nulo ou não pode ser nulo se id_pessoa_juridica estiver presente.','consistency','id_unidade_familiar','id_pessoa_juridica IS NULL OR id_unidade_familiar IS NOT NULL','warning',95.0,'fail',0.160000000000000003,11377318,18094,11359224,NULL,'2025-09-17 12:18:38.7524506');
INSERT INTO DataQualityResults VALUES(90,10,'rule_001','Verificar id_documento não nulo','O campo id_documento não pode ser nulo.','completeness','id_documento','id_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:26:30.3039885');
INSERT INTO DataQualityResults VALUES(91,10,'rule_002','Verificar nm_documento não nulo','O campo nm_documento não pode ser nulo ou vazio.','completeness','nm_documento','LENGTH(TRIM(nm_documento::text)) > 0','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:26:31.8314533');
INSERT INTO DataQualityResults VALUES(92,10,'rule_003','Verificar cd_cloud_storage não nulo','O campo cd_cloud_storage não pode ser nulo.','completeness','cd_cloud_storage','cd_cloud_storage IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:26:32.039802');
INSERT INTO DataQualityResults VALUES(93,10,'rule_004','Verificar id_tipo_documento não nulo','O campo id_tipo_documento não pode ser nulo.','completeness','id_tipo_documento','id_tipo_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:26:32.2416197');
INSERT INTO DataQualityResults VALUES(94,10,'rule_005','Verificar unicidade de id_documento','O campo id_documento deve ser único.','uniqueness','id_documento','id_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:26:32.4491339');
INSERT INTO DataQualityResults VALUES(95,10,'rule_006','Verificar formato de ds_url','O campo ds_url deve ter um formato de URL válido.','validity','ds_url','ds_url ~* ''^https?://.*''','warning',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:26:33.5858676');
INSERT INTO DataQualityResults VALUES(96,10,'rule_007','Verificar data de criação não futura','O campo dt_criacao não pode ser uma data futura.','timeliness','dt_criacao','dt_criacao <= CURRENT_DATE','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:26:34.554464');
INSERT INTO DataQualityResults VALUES(97,10,'rule_008','Verificar consistência entre dt_criacao e dt_atualizacao','A data de atualização deve ser maior ou igual à data de criação, se não for nula.','consistency','dt_atualizacao','dt_atualizacao >= dt_criacao OR dt_atualizacao IS NULL','warning',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:26:35.4611948');
INSERT INTO DataQualityResults VALUES(98,10,'rule_009','Verificar id_unidade_familiar se id_pessoa_juridica é nulo','O campo id_unidade_familiar deve ser nulo se id_pessoa_juridica não for nulo.','consistency','id_unidade_familiar','id_pessoa_juridica IS NULL OR id_unidade_familiar IS NOT NULL','warning',95.0,'fail',0.160000000000000003,11377318,18094,11359224,NULL,'2025-09-17 12:26:36.3203685');
INSERT INTO DataQualityResults VALUES(99,10,'rule_010','Verificar nm_extensao se não nulo','O campo nm_extensao deve ter um valor válido se não for nulo.','validity','nm_extensao','nm_extensao IS NULL OR LENGTH(TRIM(nm_extensao::text)) > 0','info',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:26:37.5099143');
INSERT INTO DataQualityResults VALUES(100,11,'rule_001','Verificação de id_documento não nulo','O campo id_documento não deve ser nulo.','completeness','id_documento','id_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:34:23.7510617');
INSERT INTO DataQualityResults VALUES(101,11,'rule_002','Verificação de nm_documento não nulo','O campo nm_documento não deve ser nulo ou vazio.','completeness','nm_documento','LENGTH(TRIM(nm_documento::text)) > 0','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:34:25.1849637');
INSERT INTO DataQualityResults VALUES(102,11,'rule_003','Verificação de cd_cloud_storage não nulo','O campo cd_cloud_storage não deve ser nulo.','completeness','cd_cloud_storage','cd_cloud_storage IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:34:25.3758395');
INSERT INTO DataQualityResults VALUES(103,11,'rule_004','Verificação de id_tipo_documento não nulo','O campo id_tipo_documento não deve ser nulo.','completeness','id_tipo_documento','id_tipo_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:34:25.6062324');
INSERT INTO DataQualityResults VALUES(104,11,'rule_005','Verificação de unicidade de id_documento','O campo id_documento deve ser único.','uniqueness','id_documento','id_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:34:25.8048493');
INSERT INTO DataQualityResults VALUES(105,11,'rule_006','Verificação de formato de ds_url','O campo ds_url deve ter um formato de URL válido.','validity','ds_url','ds_url ~* ''^https?://.*$''','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:34:31.5740108');
INSERT INTO DataQualityResults VALUES(106,11,'rule_007','Verificação de dt_criacao não futura','O campo dt_criacao não deve ser uma data futura.','timeliness','dt_criacao','dt_criacao <= CURRENT_DATE','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:34:32.4881991');
INSERT INTO DataQualityResults VALUES(107,11,'rule_008','Verificação de dt_atualizacao não futura','O campo dt_atualizacao não deve ser uma data futura.','timeliness','dt_atualizacao','dt_atualizacao IS NULL OR dt_atualizacao <= CURRENT_DATE','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:34:33.3864219');
INSERT INTO DataQualityResults VALUES(108,11,'rule_009','Verificação de id_unidade_familiar quando id_pessoa_juridica é nulo','O campo id_unidade_familiar deve ser nulo se id_pessoa_juridica não estiver presente.','consistency','id_unidade_familiar','id_pessoa_juridica IS NULL OR id_unidade_familiar IS NOT NULL','warning',95.0,'fail',0.160000000000000003,11377318,18094,11359224,NULL,'2025-09-17 12:34:34.2319905');
INSERT INTO DataQualityResults VALUES(109,11,'rule_010','Verificação de nm_extensao quando não nulo','O campo nm_extensao deve ser nulo ou ter um valor válido.','validity','nm_extensao','nm_extensao IS NULL OR LENGTH(TRIM(nm_extensao::text)) > 0','warning',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:34:35.4232007');
INSERT INTO DataQualityResults VALUES(110,12,'rule_001','Verificação de id_documento não nulo','O campo id_documento não pode ser nulo.','completeness','id_documento','id_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:37:01.0346637');
INSERT INTO DataQualityResults VALUES(111,12,'rule_002','Verificação de nm_documento não nulo','O campo nm_documento não pode ser nulo ou vazio.','completeness','nm_documento','LENGTH(TRIM(nm_documento::text)) > 0','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:37:00.0738629');
INSERT INTO DataQualityResults VALUES(112,12,'rule_003','Verificação de cd_cloud_storage não nulo','O campo cd_cloud_storage não pode ser nulo.','completeness','cd_cloud_storage','cd_cloud_storage IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:37:00.2881396');
INSERT INTO DataQualityResults VALUES(113,12,'rule_004','Verificação de id_tipo_documento não nulo','O campo id_tipo_documento não pode ser nulo.','completeness','id_tipo_documento','id_tipo_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:37:00.4911678');
INSERT INTO DataQualityResults VALUES(114,12,'rule_005','Verificação de unicidade de id_documento','O campo id_documento deve ser único.','uniqueness','id_documento','id_documento IS NOT NULL','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:37:00.7210394');
INSERT INTO DataQualityResults VALUES(115,12,'rule_006','Verificação de formato de ds_url','O campo ds_url deve ter um formato de URL válido.','validity','ds_url','ds_url ~* ''^https?://.*$''','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:37:06.3422419');
INSERT INTO DataQualityResults VALUES(116,12,'rule_007','Verificação de dt_criacao não futura','O campo dt_criacao não pode ser uma data futura.','timeliness','dt_criacao','dt_criacao <= CURRENT_DATE','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:37:07.2202452');
INSERT INTO DataQualityResults VALUES(117,12,'rule_008','Verificação de dt_atualizacao não futura','O campo dt_atualizacao não pode ser uma data futura.','timeliness','dt_atualizacao','dt_atualizacao IS NULL OR dt_atualizacao <= CURRENT_DATE','error',95.0,'fail',0.0,11377318,0,11377318,NULL,'2025-09-17 12:37:08.0732072');
INSERT INTO DataQualityResults VALUES(118,12,'rule_009','Verificação de id_unidade_familiar quando id_pessoa_juridica é nulo','O campo id_unidade_familiar deve ser nulo se id_pessoa_juridica não for nulo.','consistency','id_unidade_familiar','id_pessoa_juridica IS NULL OR id_unidade_familiar IS NOT NULL','warning',95.0,'fail',0.160000000000000003,11377318,18094,11359224,NULL,'2025-09-17 12:37:08.9003726');
INSERT INTO DataQualityResults VALUES(119,12,'rule_010','Verificação de nm_extensao quando não nulo','O campo nm_extensao deve ser nulo se o arquivo não foi migrado para a nuvem.','consistency','nm_extensao','st_migrado_nuvem = true OR nm_extensao IS NULL','warning',95.0,'fail',27.5300000000000011,11377318,3132029,8245289,NULL,'2025-09-17 12:37:09.8400478');
INSERT INTO DataQualityResults VALUES(120,13,'rule_001','Verificação de id_documento não nulo','O campo id_documento não pode ser nulo.','completeness','id_documento','id_documento IS NOT NULL','error',95.0,'pass',100.0,11377318,11377318,0,NULL,'2025-09-17 12:48:52.982124');
INSERT INTO DataQualityResults VALUES(121,13,'rule_002','Verificação de nm_documento não nulo','O campo nm_documento não pode ser nulo ou vazio.','completeness','nm_documento','LENGTH(TRIM(nm_documento::text)) > 0','error',95.0,'pass',100.0,11377318,11377318,0,NULL,'2025-09-17 12:48:54.3646324');
INSERT INTO DataQualityResults VALUES(122,13,'rule_003','Verificação de cd_cloud_storage não nulo','O campo cd_cloud_storage não pode ser nulo.','completeness','cd_cloud_storage','cd_cloud_storage IS NOT NULL','error',95.0,'pass',100.0,11377318,11377318,0,NULL,'2025-09-17 12:48:54.5662546');
INSERT INTO DataQualityResults VALUES(123,13,'rule_004','Verificação de id_tipo_documento não nulo','O campo id_tipo_documento não pode ser nulo.','completeness','id_tipo_documento','id_tipo_documento IS NOT NULL','error',95.0,'pass',100.0,11377318,11377318,0,NULL,'2025-09-17 12:48:54.7509981');
INSERT INTO DataQualityResults VALUES(124,13,'rule_005','Verificação de unicidade de id_documento','O campo id_documento deve ser único.','uniqueness','id_documento','id_documento IS NOT NULL','error',95.0,'pass',100.0,11377318,11377318,0,NULL,'2025-09-17 12:48:54.9440203');
INSERT INTO DataQualityResults VALUES(125,13,'rule_006','Verificação de formato de ds_url','O campo ds_url deve ter um formato de URL válido.','validity','ds_url','ds_url ~* ''^https?://.*$''','warning',95.0,'pass',100.0,11377318,11377318,0,NULL,'2025-09-17 12:49:00.484601');
INSERT INTO DataQualityResults VALUES(126,13,'rule_007','Verificação de dt_criacao não futura','O campo dt_criacao não pode ser uma data futura.','timeliness','dt_criacao','dt_criacao <= CURRENT_DATE','error',95.0,'pass',100.0,11377318,11377318,0,NULL,'2025-09-17 12:49:01.3285111');
INSERT INTO DataQualityResults VALUES(127,13,'rule_008','Verificação de dt_atualizacao não futura','O campo dt_atualizacao não pode ser uma data futura.','timeliness','dt_atualizacao','dt_atualizacao IS NULL OR dt_atualizacao <= CURRENT_DATE','warning',95.0,'pass',100.0,11377318,11377318,0,NULL,'2025-09-17 12:49:02.1655062');
INSERT INTO DataQualityResults VALUES(128,13,'rule_009','Verificação de id_unidade_familiar quando id_tipo_documento é 6','O campo id_unidade_familiar deve ser preenchido quando id_tipo_documento é 6.','consistency','id_unidade_familiar','id_tipo_documento = 6 OR id_unidade_familiar IS NOT NULL','error',95.0,'pass',99.9200000000000017,11377318,11368070,9248,NULL,'2025-09-17 12:49:02.855507');
INSERT INTO DataQualityResults VALUES(129,13,'rule_010','Verificação de nm_extensao quando não nulo','O campo nm_extensao deve ser preenchido se o arquivo foi migrado para a nuvem.','consistency','nm_extensao','st_migrado_nuvem = true OR nm_extensao IS NOT NULL','warning',95.0,'pass',100.0,11377318,11377299,19,NULL,'2025-09-17 12:49:03.9150449');
CREATE TABLE IF NOT EXISTS "CustomDataQualityRules" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_CustomDataQualityRules" PRIMARY KEY AUTOINCREMENT,
    "UserId" INTEGER NOT NULL,
    "ProfileId" INTEGER NOT NULL,
    "Schema" TEXT NOT NULL,
    "TableName" TEXT NOT NULL,
    "RuleId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "Dimension" TEXT NOT NULL,
    "Column" TEXT NOT NULL,
    "SqlCondition" TEXT NOT NULL,
    "Severity" TEXT NOT NULL,
    "ExpectedPassRate" REAL NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NULL,
    "IsActive" INTEGER NOT NULL,
    "Source" TEXT NOT NULL,
    "Notes" TEXT NULL, "ChangeReason" TEXT NULL, "IsLatestVersion" INTEGER NOT NULL DEFAULT 1, "Version" INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT "FK_CustomDataQualityRules_Profiles_ProfileId" FOREIGN KEY ("ProfileId") REFERENCES "Profiles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_CustomDataQualityRules_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "dq_column_metrics" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_dq_column_metrics" PRIMARY KEY AUTOINCREMENT,
    "SchemaName" TEXT NOT NULL,
    "TableName" TEXT NOT NULL,
    "ColumnName" TEXT NOT NULL,
    "MetricName" TEXT NOT NULL,
    "MetricValue" TEXT NULL,
    "CollectedAt" TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS "dq_preflight_results" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_dq_preflight_results" PRIMARY KEY AUTOINCREMENT,
    "SchemaName" TEXT NOT NULL,
    "TableName" TEXT NULL,
    "TestType" TEXT NOT NULL,
    "TestName" TEXT NOT NULL,
    "SqlExecuted" TEXT NOT NULL,
    "Expectation" TEXT NULL,
    "Success" INTEGER NOT NULL,
    "ResultData" TEXT NULL,
    "ErrorMessage" TEXT NULL,
    "ExecutedAt" TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS "dq_rule_candidates" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_dq_rule_candidates" PRIMARY KEY AUTOINCREMENT,
    "SchemaName" TEXT NOT NULL,
    "TableName" TEXT NOT NULL,
    "ColumnName" TEXT NULL,
    "Dimension" TEXT NOT NULL,
    "RuleName" TEXT NOT NULL,
    "CheckSql" TEXT NOT NULL,
    "Description" TEXT NULL,
    "Severity" TEXT NOT NULL,
    "AutoGenerated" INTEGER NOT NULL,
    "ApprovedByUser" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS "dq_table_metrics" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_dq_table_metrics" PRIMARY KEY AUTOINCREMENT,
    "SchemaName" TEXT NOT NULL,
    "TableName" TEXT NOT NULL,
    "MetricGroup" TEXT NOT NULL,
    "MetricName" TEXT NOT NULL,
    "MetricValue" TEXT NULL,
    "CollectedAt" TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS "dq_rule_executions" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_dq_rule_executions" PRIMARY KEY AUTOINCREMENT,
    "RuleCandidateId" INTEGER NOT NULL,
    "TotalRecords" INTEGER NOT NULL,
    "ValidRecords" INTEGER NOT NULL,
    "InvalidRecords" INTEGER NOT NULL,
    "Success" INTEGER NOT NULL,
    "ErrorMessage" TEXT NULL,
    "ExecutionTimeMs" INTEGER NULL,
    "ExecutedAt" TEXT NOT NULL,
    CONSTRAINT "FK_dq_rule_executions_dq_rule_candidates_RuleCandidateId" FOREIGN KEY ("RuleCandidateId") REFERENCES "dq_rule_candidates" ("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "TableEssentialMetrics" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_TableEssentialMetrics" PRIMARY KEY AUTOINCREMENT,
    "UserId" INTEGER NOT NULL,
    "ProfileId" INTEGER NOT NULL,
    "Schema" TEXT NOT NULL,
    "TableName" TEXT NOT NULL,
    "CollectedAt" TEXT NOT NULL,
    "TotalRows" INTEGER NOT NULL,
    "EstimatedSizeBytes" INTEGER NOT NULL,
    "TotalColumns" INTEGER NOT NULL,
    "ColumnsWithNulls" INTEGER NOT NULL,
    "OverallCompleteness" REAL NOT NULL,
    "DuplicateRows" INTEGER NOT NULL,
    "DuplicationRate" REAL NOT NULL,
    "PrimaryKeyColumns" TEXT NULL,
    CONSTRAINT "FK_TableEssentialMetrics_Profiles_ProfileId" FOREIGN KEY ("ProfileId") REFERENCES "Profiles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_TableEssentialMetrics_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ColumnEssentialMetrics" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ColumnEssentialMetrics" PRIMARY KEY AUTOINCREMENT,
    "TableMetricsId" INTEGER NOT NULL,
    "ColumnName" TEXT NOT NULL,
    "DataType" TEXT NOT NULL,
    "IsNullable" INTEGER NOT NULL,
    "TotalValues" INTEGER NOT NULL,
    "NullValues" INTEGER NOT NULL,
    "EmptyValues" INTEGER NOT NULL,
    "CompletenessRate" REAL NOT NULL,
    "UniqueValues" INTEGER NOT NULL,
    "CardinalityRate" REAL NOT NULL,
    "MinNumeric" TEXT NULL,
    "MaxNumeric" TEXT NULL,
    "AvgNumeric" TEXT NULL,
    "StdDevNumeric" TEXT NULL,
    "MinDate" TEXT NULL,
    "MaxDate" TEXT NULL,
    "MinLength" INTEGER NULL,
    "MaxLength" INTEGER NULL,
    "AvgLength" REAL NULL,
    "TopValuesJson" TEXT NULL,
    CONSTRAINT "FK_ColumnEssentialMetrics_TableEssentialMetrics_TableMetricsId" FOREIGN KEY ("TableMetricsId") REFERENCES "TableEssentialMetrics" ("Id") ON DELETE CASCADE
);
DELETE FROM sqlite_sequence;
INSERT INTO sqlite_sequence VALUES('Users',5);
INSERT INTO sqlite_sequence VALUES('Profiles',3);
INSERT INTO sqlite_sequence VALUES('UserApiSettings',1);
INSERT INTO sqlite_sequence VALUES('DataQualityAnalyses',13);
INSERT INTO sqlite_sequence VALUES('DataQualityResults',129);
CREATE INDEX "IX_Profiles_UserId" ON "Profiles" ("UserId");
CREATE UNIQUE INDEX "IX_Reports_UserId_Kind_InputSignature" ON "Reports" ("UserId", "Kind", "InputSignature");
CREATE UNIQUE INDEX "IX_Users_Username" ON "Users" ("Username");
CREATE UNIQUE INDEX "IX_UserApiSettings_UserId_Provider" ON "UserApiSettings" ("UserId", "Provider");
CREATE INDEX "IX_DataQualityAnalyses_ProfileId" ON "DataQualityAnalyses" ("ProfileId");
CREATE INDEX "IX_DataQualityAnalyses_UserId" ON "DataQualityAnalyses" ("UserId");
CREATE INDEX "IX_DataQualityResults_AnalysisId" ON "DataQualityResults" ("AnalysisId");
CREATE INDEX "IX_CustomDataQualityRules_ProfileId" ON "CustomDataQualityRules" ("ProfileId");
CREATE UNIQUE INDEX "IX_CustomDataQualityRule_UniqueLatestVersion" ON "CustomDataQualityRules" ("UserId", "ProfileId", "Schema", "TableName", "RuleId", "IsLatestVersion") WHERE IsLatestVersion = 1;
CREATE UNIQUE INDEX "IX_CustomDataQualityRule_UniqueRule" ON "CustomDataQualityRules" ("UserId", "ProfileId", "Schema", "TableName", "RuleId", "Version");
CREATE INDEX "IX_dq_rule_executions_RuleCandidateId" ON "dq_rule_executions" ("RuleCandidateId");
CREATE INDEX "IX_ColumnEssentialMetrics_TableMetricsId" ON "ColumnEssentialMetrics" ("TableMetricsId");
CREATE INDEX "IX_TableEssentialMetrics_ProfileId" ON "TableEssentialMetrics" ("ProfileId");
CREATE INDEX "IX_TableEssentialMetrics_UniqueTable" ON "TableEssentialMetrics" ("UserId", "ProfileId", "Schema", "TableName");
COMMIT;
