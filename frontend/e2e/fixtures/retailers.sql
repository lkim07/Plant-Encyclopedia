-- End-to-end test shops (design 2026-10-27). FAKE: these shops do not exist. Loaded only into the throwaway
-- database of docker-compose.e2e.yml, so Where to Buy has shops near Vancouver and Kamloops without an
-- OpenStreetMap import. Source "e2e-fixture"; no websites or addresses, so nothing points at a real business.
BEGIN;

DO $$
BEGIN
    IF current_database() <> 'plant_encyclopedia_test' THEN
        RAISE EXCEPTION 'End-to-end test shops must not be loaded into database "%".', current_database();
    END IF;
END $$;

INSERT INTO retailers (id, source, source_id, name, kind, street, city, region, postcode, website, latitude, longitude, retrieved_at) VALUES
    ('e2e00000-0000-4000-8000-000000000001', 'e2e-fixture', 'van-1', 'Test Nursery (Vancouver)',       'Nursery',      NULL, 'Vancouver', 'BC', NULL, NULL, 49.2790, -123.1150, '2026-10-27T00:00:00Z'),
    ('e2e00000-0000-4000-8000-000000000002', 'e2e-fixture', 'van-2', 'Test Garden Centre (Vancouver)', 'GardenCentre', NULL, 'Vancouver', 'BC', NULL, NULL, 49.2660, -123.1400, '2026-10-27T00:00:00Z'),
    ('e2e00000-0000-4000-8000-000000000003', 'e2e-fixture', 'van-3', 'Test Flower Shop (Vancouver)',   'FlowerShop',   NULL, 'Vancouver', 'BC', NULL, NULL, 49.2840, -123.1210, '2026-10-27T00:00:00Z'),
    ('e2e00000-0000-4000-8000-000000000004', 'e2e-fixture', 'kam-1', 'Test Nursery (Kamloops)',        'Nursery',      NULL, 'Kamloops',  'BC', NULL, NULL, 50.6740, -120.3270, '2026-10-27T00:00:00Z'),
    ('e2e00000-0000-4000-8000-000000000005', 'e2e-fixture', 'kam-2', 'Test Garden Centre (Kamloops)',  'GardenCentre', NULL, 'Kamloops',  'BC', NULL, NULL, 50.6850, -120.3500, '2026-10-27T00:00:00Z'),
    ('e2e00000-0000-4000-8000-000000000006', 'e2e-fixture', 'kam-3', 'Test Flower Shop (Kamloops)',    'FlowerShop',   NULL, 'Kamloops',  'BC', NULL, NULL, 50.6700, -120.3330, '2026-10-27T00:00:00Z')
ON CONFLICT DO NOTHING;

COMMIT;
