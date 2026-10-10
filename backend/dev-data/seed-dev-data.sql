-- =============================================================================
-- Plant Encyclopedia - DEVELOPMENT SAMPLE DATA
-- =============================================================================
-- Purpose : small dataset for local development and automated tests only.
-- Content : well-known plant names and basic classification. NOT verified and
--           NOT authoritative. Every plant description says so.
--           No sources, care data or images: none have been verified yet, and
--           provenance must never be invented.
-- Safety  : runs only on plant_encyclopedia_dev or plant_encyclopedia_test.
--           One transaction. Only INSERTs with fixed IDs and ON CONFLICT DO NOTHING,
--           so re-running adds nothing and never updates or deletes existing rows.
-- Run     : copy into the container, then run with psql (keeps UTF-8 text intact):
--   docker compose cp backend/dev-data/seed-dev-data.sql postgres:/tmp/seed-dev-data.sql
--   docker compose exec -T postgres psql -U plant_admin -d plant_encyclopedia_dev -v ON_ERROR_STOP=1 -f /tmp/seed-dev-data.sql
-- =============================================================================

BEGIN;

DO $$
BEGIN
    IF current_database() NOT IN ('plant_encyclopedia_dev', 'plant_encyclopedia_test') THEN
        RAISE EXCEPTION 'Development sample data must not be loaded into database "%".', current_database();
    END IF;
END $$;

-- Categories (the six product categories) -------------------------------------
INSERT INTO categories (id, name, slug) VALUES
    ('d5c00000-0000-4000-8000-000000000001', 'Flowers',    'flowers'),
    ('d5c00000-0000-4000-8000-000000000002', 'Trees',      'trees'),
    ('d5c00000-0000-4000-8000-000000000003', 'Fruits',     'fruits'),
    ('d5c00000-0000-4000-8000-000000000004', 'Vegetables', 'vegetables'),
    ('d5c00000-0000-4000-8000-000000000005', 'Herbs',      'herbs'),
    ('d5c00000-0000-4000-8000-000000000006', 'Succulents', 'succulents')
ON CONFLICT DO NOTHING;

-- Plant groups ----------------------------------------------------------------
INSERT INTO plant_groups (id, category_id, name, slug) VALUES
    ('d5a00000-0000-4000-8000-000000000001', 'd5c00000-0000-4000-8000-000000000001', 'Roses',     'roses'),
    ('d5a00000-0000-4000-8000-000000000002', 'd5c00000-0000-4000-8000-000000000001', 'Tulips',    'tulips'),
    ('d5a00000-0000-4000-8000-000000000003', 'd5c00000-0000-4000-8000-000000000002', 'Maples',    'maples'),
    ('d5a00000-0000-4000-8000-000000000004', 'd5c00000-0000-4000-8000-000000000002', 'Oaks',      'oaks'),
    ('d5a00000-0000-4000-8000-000000000005', 'd5c00000-0000-4000-8000-000000000003', 'Apples',    'apples'),
    ('d5a00000-0000-4000-8000-000000000006', 'd5c00000-0000-4000-8000-000000000003', 'Citrus',    'citrus'),
    ('d5a00000-0000-4000-8000-000000000007', 'd5c00000-0000-4000-8000-000000000004', 'Tomatoes',  'tomatoes'),
    ('d5a00000-0000-4000-8000-000000000008', 'd5c00000-0000-4000-8000-000000000004', 'Peppers',   'peppers'),
    ('d5a00000-0000-4000-8000-000000000009', 'd5c00000-0000-4000-8000-000000000005', 'Basil',     'basil'),
    ('d5a00000-0000-4000-8000-000000000010', 'd5c00000-0000-4000-8000-000000000005', 'Mint',      'mint'),
    ('d5a00000-0000-4000-8000-000000000011', 'd5c00000-0000-4000-8000-000000000006', 'Echeveria', 'echeveria'),
    ('d5a00000-0000-4000-8000-000000000012', 'd5c00000-0000-4000-8000-000000000006', 'Aloe',      'aloe')
ON CONFLICT DO NOTHING;

-- Taxonomy (one row per species; only kingdom, family, genus, species) ---------
-- "species" holds the full binomial. "Rosa x hybrida" is a horticultural name for
-- modern garden roses, used here as in the project documentation.
INSERT INTO taxonomies (id, kingdom, family, genus, species) VALUES
    ('d5b00000-0000-4000-8000-000000000001', 'Plantae', 'Rosaceae',       'Rosa',      'Rosa × hybrida'),
    ('d5b00000-0000-4000-8000-000000000002', 'Plantae', 'Liliaceae',      'Tulipa',    'Tulipa gesneriana'),
    ('d5b00000-0000-4000-8000-000000000003', 'Plantae', 'Sapindaceae',    'Acer',      'Acer palmatum'),
    ('d5b00000-0000-4000-8000-000000000004', 'Plantae', 'Sapindaceae',    'Acer',      'Acer saccharum'),
    ('d5b00000-0000-4000-8000-000000000005', 'Plantae', 'Fagaceae',       'Quercus',   'Quercus robur'),
    ('d5b00000-0000-4000-8000-000000000006', 'Plantae', 'Rosaceae',       'Malus',     'Malus domestica'),
    ('d5b00000-0000-4000-8000-000000000007', 'Plantae', 'Rutaceae',       'Citrus',    'Citrus × limon'),
    ('d5b00000-0000-4000-8000-000000000008', 'Plantae', 'Solanaceae',     'Solanum',   'Solanum lycopersicum'),
    ('d5b00000-0000-4000-8000-000000000009', 'Plantae', 'Solanaceae',     'Capsicum',  'Capsicum annuum'),
    ('d5b00000-0000-4000-8000-000000000010', 'Plantae', 'Lamiaceae',      'Ocimum',    'Ocimum basilicum'),
    ('d5b00000-0000-4000-8000-000000000011', 'Plantae', 'Lamiaceae',      'Mentha',    'Mentha spicata'),
    ('d5b00000-0000-4000-8000-000000000012', 'Plantae', 'Crassulaceae',   'Echeveria', 'Echeveria elegans'),
    ('d5b00000-0000-4000-8000-000000000013', 'Plantae', 'Asphodelaceae',  'Aloe',      'Aloe vera')
ON CONFLICT DO NOTHING;

-- Plants: 16 Published (each with a group) + 1 Draft without a group ----------
INSERT INTO plants (id, display_name, scientific_name, cultivar_name, description, plant_group_id, taxonomy_id, status) VALUES
    ('d5e00000-0000-4000-8000-000000000001', 'Rosa ''Peace''',          'Rosa × hybrida',       'Peace',          'Development sample data — not verified.', 'd5a00000-0000-4000-8000-000000000001', 'd5b00000-0000-4000-8000-000000000001', 'Published'),
    ('d5e00000-0000-4000-8000-000000000002', 'Rosa ''Iceberg''',        'Rosa × hybrida',       'Iceberg',        'Development sample data — not verified.', 'd5a00000-0000-4000-8000-000000000001', 'd5b00000-0000-4000-8000-000000000001', 'Published'),
    ('d5e00000-0000-4000-8000-000000000003', 'Rosa ''Mr. Lincoln''',    'Rosa × hybrida',       'Mr. Lincoln',    'Development sample data — not verified.', 'd5a00000-0000-4000-8000-000000000001', 'd5b00000-0000-4000-8000-000000000001', 'Published'),
    ('d5e00000-0000-4000-8000-000000000004', 'Garden Tulip',            'Tulipa gesneriana',    NULL,             'Development sample data — not verified.', 'd5a00000-0000-4000-8000-000000000002', 'd5b00000-0000-4000-8000-000000000002', 'Published'),
    ('d5e00000-0000-4000-8000-000000000005', 'Japanese Maple',          'Acer palmatum',        NULL,             'Development sample data — not verified.', 'd5a00000-0000-4000-8000-000000000003', 'd5b00000-0000-4000-8000-000000000003', 'Published'),
    ('d5e00000-0000-4000-8000-000000000006', 'Sugar Maple',             'Acer saccharum',       NULL,             'Development sample data — not verified.', 'd5a00000-0000-4000-8000-000000000003', 'd5b00000-0000-4000-8000-000000000004', 'Published'),
    ('d5e00000-0000-4000-8000-000000000007', 'English Oak',             'Quercus robur',        NULL,             'Development sample data — not verified.', 'd5a00000-0000-4000-8000-000000000004', 'd5b00000-0000-4000-8000-000000000005', 'Published'),
    ('d5e00000-0000-4000-8000-000000000008', 'Malus ''Honeycrisp''',    'Malus domestica',      'Honeycrisp',     'Development sample data — not verified.', 'd5a00000-0000-4000-8000-000000000005', 'd5b00000-0000-4000-8000-000000000006', 'Published'),
    ('d5e00000-0000-4000-8000-000000000009', 'Malus ''Granny Smith''',  'Malus domestica',      'Granny Smith',   'Development sample data — not verified.', 'd5a00000-0000-4000-8000-000000000005', 'd5b00000-0000-4000-8000-000000000006', 'Published'),
    ('d5e00000-0000-4000-8000-000000000010', 'Lemon',                   'Citrus × limon',       NULL,             'Development sample data — not verified.', 'd5a00000-0000-4000-8000-000000000006', 'd5b00000-0000-4000-8000-000000000007', 'Published'),
    ('d5e00000-0000-4000-8000-000000000011', 'Tomato',                  'Solanum lycopersicum', NULL,             'Development sample data — not verified.', 'd5a00000-0000-4000-8000-000000000007', 'd5b00000-0000-4000-8000-000000000008', 'Published'),
    ('d5e00000-0000-4000-8000-000000000012', 'Bell Pepper',             'Capsicum annuum',      NULL,             'Development sample data — not verified.', 'd5a00000-0000-4000-8000-000000000008', 'd5b00000-0000-4000-8000-000000000009', 'Published'),
    ('d5e00000-0000-4000-8000-000000000013', 'Sweet Basil',             'Ocimum basilicum',     NULL,             'Development sample data — not verified.', 'd5a00000-0000-4000-8000-000000000009', 'd5b00000-0000-4000-8000-000000000010', 'Published'),
    ('d5e00000-0000-4000-8000-000000000014', 'Spearmint',               'Mentha spicata',       NULL,             'Development sample data — not verified.', 'd5a00000-0000-4000-8000-000000000010', 'd5b00000-0000-4000-8000-000000000011', 'Published'),
    ('d5e00000-0000-4000-8000-000000000015', 'Mexican Snowball',        'Echeveria elegans',    NULL,             'Development sample data — not verified.', 'd5a00000-0000-4000-8000-000000000011', 'd5b00000-0000-4000-8000-000000000012', 'Published'),
    ('d5e00000-0000-4000-8000-000000000016', 'Aloe Vera',               'Aloe vera',            NULL,             'Development sample data — not verified.', 'd5a00000-0000-4000-8000-000000000012', 'd5b00000-0000-4000-8000-000000000013', 'Published'),
    ('d5e00000-0000-4000-8000-000000000017', 'Rosa ''Double Delight''', 'Rosa × hybrida',       'Double Delight', 'Development sample data — not verified.', NULL,                                   'd5b00000-0000-4000-8000-000000000001', 'Draft')
ON CONFLICT DO NOTHING;

-- Alternative names: common names with language codes, and scientific synonyms --
INSERT INTO plant_names (id, plant_id, name, name_type, language_code) VALUES
    ('d5f00000-0000-4000-8000-000000000001', 'd5e00000-0000-4000-8000-000000000001', 'Peace Rose',              'Common',  'en'),
    ('d5f00000-0000-4000-8000-000000000002', 'd5e00000-0000-4000-8000-000000000001', 'Madame A. Meilland',      'Common',  'fr'),
    ('d5f00000-0000-4000-8000-000000000003', 'd5e00000-0000-4000-8000-000000000001', 'Gloria Dei',              'Common',  'de'),
    ('d5f00000-0000-4000-8000-000000000004', 'd5e00000-0000-4000-8000-000000000002', 'Schneewittchen',          'Common',  'de'),
    ('d5f00000-0000-4000-8000-000000000005', 'd5e00000-0000-4000-8000-000000000004', 'Tulip',                   'Common',  'en'),
    ('d5f00000-0000-4000-8000-000000000006', 'd5e00000-0000-4000-8000-000000000011', '토마토',                  'Common',  'ko'),
    ('d5f00000-0000-4000-8000-000000000007', 'd5e00000-0000-4000-8000-000000000011', 'Lycopersicon esculentum', 'Synonym', NULL),
    ('d5f00000-0000-4000-8000-000000000008', 'd5e00000-0000-4000-8000-000000000012', 'Sweet Pepper',            'Common',  'en'),
    ('d5f00000-0000-4000-8000-000000000009', 'd5e00000-0000-4000-8000-000000000013', 'Basil',                   'Common',  'en'),
    ('d5f00000-0000-4000-8000-000000000010', 'd5e00000-0000-4000-8000-000000000013', '바질',                    'Common',  'ko'),
    ('d5f00000-0000-4000-8000-000000000011', 'd5e00000-0000-4000-8000-000000000016', '알로에',                  'Common',  'ko'),
    ('d5f00000-0000-4000-8000-000000000012', 'd5e00000-0000-4000-8000-000000000016', 'Aloe barbadensis',        'Synonym', NULL)
ON CONFLICT DO NOTHING;

COMMIT;
