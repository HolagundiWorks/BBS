-- ============================================================================
-- AQC-Core — estimation linked-item data model (logical ERD / schema)
--
-- Relational view of the model the app persists as JSON (.bbsproj, version 17).
-- Two item relationships: derivation (trade -> link_rule -> derived_item) and
-- composition (trade / mix_design -> material).
--
-- Portable ANSI-ish DDL. Import into a Database Schema Designer:
--   DBeaver / MySQL Workbench (reverse engineer) / SSMS / pgAdmin / dbdiagram.io
-- Adjust types per dialect if needed (DOUBLE PRECISION, BOOLEAN, VARCHAR).
-- ============================================================================

-- ---- Reference / lookup -----------------------------------------------------

CREATE TABLE trade (
    key            VARCHAR(64)  NOT NULL,   -- = CivilLine.Element, e.g. 'Masonry'
    display        VARCHAR(128),
    unit           VARCHAR(16),             -- m2 | m3 | m | nos
    default_basis  VARCHAR(16),             -- Area | Volume | Length | Perimeter | Count
    CONSTRAINT pk_trade PRIMARY KEY (key)
);

-- ---- Project & measurement --------------------------------------------------

CREATE TABLE project (
    id              VARCHAR(64) NOT NULL,
    name            VARCHAR(255),
    format_version  INTEGER,                -- bbsproj schema version (17)
    CONSTRAINT pk_project PRIMARY KEY (id)
);

CREATE TABLE level (
    id                 VARCHAR(64) NOT NULL,
    project_id         VARCHAR(64) NOT NULL,
    name               VARCHAR(128),
    height_mm          INTEGER,
    slab_thickness_mm  INTEGER,
    beam_depth_mm      INTEGER,
    CONSTRAINT pk_level PRIMARY KEY (id),
    CONSTRAINT fk_level_project FOREIGN KEY (project_id) REFERENCES project (id)
);

CREATE TABLE takeoff_item (
    id           VARCHAR(64) NOT NULL,
    project_id   VARCHAR(64) NOT NULL,
    trade_key    VARCHAR(64) NOT NULL,      -- which trade sheet this row belongs to
    mark         VARCHAR(64),
    level_id     VARCHAR(64),
    length_m     DOUBLE PRECISION,
    breadth_m    DOUBLE PRECISION,
    height_m     DOUBLE PRECISION,
    area_m2      DOUBLE PRECISION,
    volume_m3    DOUBLE PRECISION,
    qty          DOUBLE PRECISION,
    unit         VARCHAR(16),
    source       VARCHAR(32),               -- manual | auto_wall | from_plaster | ...
    source_mark  VARCHAR(64),               -- provenance: parent item
    notes        TEXT,
    CONSTRAINT pk_takeoff_item PRIMARY KEY (id),
    CONSTRAINT fk_takeoff_project FOREIGN KEY (project_id) REFERENCES project (id),
    CONSTRAINT fk_takeoff_trade   FOREIGN KEY (trade_key)  REFERENCES trade (key),
    CONSTRAINT fk_takeoff_level   FOREIGN KEY (level_id)   REFERENCES level (id)
);

-- ---- Linked-item derivation engine ------------------------------------------

CREATE TABLE link_rule (
    id            VARCHAR(64) NOT NULL,
    project_id    VARCHAR(64) NOT NULL,
    name          VARCHAR(255),
    enabled       BOOLEAN,
    source_trade  VARCHAR(64) NOT NULL,     -- producer
    target_trade  VARCHAR(64) NOT NULL,     -- consumer
    basis         VARCHAR(16),              -- Area | Volume | Length | Perimeter | Count
    factor        DOUBLE PRECISION,         -- target_qty = factor * source[basis]
    target_unit   VARCHAR(16),
    per_item      BOOLEAN,                  -- true: per source item; false: aggregate
    notes         TEXT,
    CONSTRAINT pk_link_rule PRIMARY KEY (id),
    CONSTRAINT fk_rule_project FOREIGN KEY (project_id)   REFERENCES project (id),
    CONSTRAINT fk_rule_source  FOREIGN KEY (source_trade) REFERENCES trade (key),
    CONSTRAINT fk_rule_target  FOREIGN KEY (target_trade) REFERENCES trade (key)
);

CREATE TABLE derived_item (
    id            VARCHAR(64) NOT NULL,
    rule_id       VARCHAR(64) NOT NULL,
    source_trade  VARCHAR(64) NOT NULL,
    source_mark   VARCHAR(64),
    level_id      VARCHAR(64),
    source_qty    DOUBLE PRECISION,
    basis         VARCHAR(16),
    factor        DOUBLE PRECISION,
    target_trade  VARCHAR(64) NOT NULL,
    target_qty    DOUBLE PRECISION,
    target_unit   VARCHAR(16),
    chained       BOOLEAN,                  -- source produced by an upstream rule
    CONSTRAINT pk_derived_item PRIMARY KEY (id),
    CONSTRAINT fk_derived_rule   FOREIGN KEY (rule_id)      REFERENCES link_rule (id),
    CONSTRAINT fk_derived_source FOREIGN KEY (source_trade) REFERENCES trade (key),
    CONSTRAINT fk_derived_target FOREIGN KEY (target_trade) REFERENCES trade (key),
    CONSTRAINT fk_derived_level  FOREIGN KEY (level_id)     REFERENCES level (id)
);

-- ---- Materials & composition (item -> material) -----------------------------

CREATE TABLE material (
    key       VARCHAR(64) NOT NULL,           -- CEMENT | FINE_AGG | COARSE_AGG | BRICK | STEEL | ...
    name      VARCHAR(255),
    unit      VARCHAR(16),                     -- bags | m3 | m | nos | kg | litre
    category  VARCHAR(32),                     -- Binder | Aggregate | Unit | Steel | Stone
    CONSTRAINT pk_material PRIMARY KEY (key)
);

CREATE TABLE mix_design (
    key         VARCHAR(64) NOT NULL,          -- CONC_M25, CM_1_6, PCC_1_4_8
    kind        VARCHAR(16),                   -- concrete | mortar | pcc
    grade       VARCHAR(32),                   -- M15..M40; mix like 1:6 for mortar
    dry_factor  DOUBLE PRECISION,              -- concrete 1.54, mortar 1.33
    note        VARCHAR(255),
    CONSTRAINT pk_mix_design PRIMARY KEY (key)
);

CREATE TABLE mix_component (
    id            VARCHAR(64) NOT NULL,
    mix_key       VARCHAR(64) NOT NULL,
    material_key  VARCHAR(64) NOT NULL,
    parts         DOUBLE PRECISION,            -- cement 1 : sand 1 : aggregate 2
    CONSTRAINT pk_mix_component PRIMARY KEY (id),
    CONSTRAINT fk_mixcomp_mix      FOREIGN KEY (mix_key)      REFERENCES mix_design (key),
    CONSTRAINT fk_mixcomp_material FOREIGN KEY (material_key) REFERENCES material (key)
);

CREATE TABLE trade_material (
    id            VARCHAR(64) NOT NULL,
    trade_key     VARCHAR(64) NOT NULL,        -- work item that consumes the material
    material_key  VARCHAR(64) NOT NULL,
    via_mix_key   VARCHAR(64),                 -- set when consumed through a mix; null for direct units
    note          VARCHAR(255),
    CONSTRAINT pk_trade_material PRIMARY KEY (id),
    CONSTRAINT fk_trademat_trade    FOREIGN KEY (trade_key)    REFERENCES trade (key),
    CONSTRAINT fk_trademat_material FOREIGN KEY (material_key) REFERENCES material (key),
    CONSTRAINT fk_trademat_mix      FOREIGN KEY (via_mix_key)  REFERENCES mix_design (key)
);

-- ---- Rates & priced estimate ------------------------------------------------

CREATE TABLE rate_book_version (
    id     VARCHAR(64) NOT NULL,
    name   VARCHAR(255),
    notes  TEXT,
    CONSTRAINT pk_rate_book_version PRIMARY KEY (id)
);

CREATE TABLE rate_item (
    id           VARCHAR(64) NOT NULL,
    version_id   VARCHAR(64) NOT NULL,
    code         VARCHAR(64),              -- MSN-BRICK-M3, PL-STD, PT-STD, ...
    category     VARCHAR(128),
    description  VARCHAR(255),
    unit         VARCHAR(16),
    rate         DOUBLE PRECISION,
    CONSTRAINT pk_rate_item PRIMARY KEY (id),
    CONSTRAINT uq_rate_item_code UNIQUE (version_id, code),
    CONSTRAINT fk_rate_item_version FOREIGN KEY (version_id) REFERENCES rate_book_version (id)
);

CREATE TABLE estimate (
    id                    VARCHAR(64) NOT NULL,
    project_id            VARCHAR(64) NOT NULL,
    rate_book_version_id  VARCHAR(64),
    base_total            DOUBLE PRECISION,
    grand_total           DOUBLE PRECISION,
    CONSTRAINT pk_estimate PRIMARY KEY (id),
    CONSTRAINT fk_estimate_project FOREIGN KEY (project_id)           REFERENCES project (id),
    CONSTRAINT fk_estimate_version FOREIGN KEY (rate_book_version_id) REFERENCES rate_book_version (id)
);

CREATE TABLE estimate_line (
    id            VARCHAR(64) NOT NULL,
    estimate_id   VARCHAR(64) NOT NULL,
    rate_item_id  VARCHAR(64),
    code          VARCHAR(64),
    category      VARCHAR(128),
    description   VARCHAR(255),
    unit          VARCHAR(16),
    qty           DOUBLE PRECISION,
    rate          DOUBLE PRECISION,
    amount        DOUBLE PRECISION,
    level_id      VARCHAR(64),
    mark          VARCHAR(64),
    length_m      DOUBLE PRECISION,
    breadth_m     DOUBLE PRECISION,
    height_m      DOUBLE PRECISION,
    area_m2       DOUBLE PRECISION,
    volume_m3     DOUBLE PRECISION,
    CONSTRAINT pk_estimate_line PRIMARY KEY (id),
    CONSTRAINT fk_line_estimate  FOREIGN KEY (estimate_id)  REFERENCES estimate (id),
    CONSTRAINT fk_line_rate_item FOREIGN KEY (rate_item_id) REFERENCES rate_item (id),
    CONSTRAINT fk_line_level     FOREIGN KEY (level_id)     REFERENCES level (id)
);
