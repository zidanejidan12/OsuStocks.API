-- Backfill historical TopPlay price impacts to the pp-relative formula:
--   impact = clamp(0.6 * playPp / playerPp, 0.005, 0.10)
--
-- Per stock, replays stock_price_history in chain order: TopPlay rows are
-- recomputed with the new formula; every other row keeps its original
-- multiplicative factor; current_price is re-synced to the final value.
--
-- playPp comes from the TopPlayDetected event payload; playerPp from the
-- player snapshot captured at/just before the event (== what the live code
-- would have used). Final price is invariant to same-cycle play pairing
-- (impacts compound multiplicatively), so row<->play pairing is cosmetic.

BEGIN;

-- Map each TopPlay history row to a play pp and the player's pp at that time.
CREATE TEMP TABLE tp_map ON COMMIT DROP AS
WITH hist AS (
    SELECT id, stock_id, created_at,
           row_number() OVER (PARTITION BY stock_id, created_at ORDER BY previous_price) AS rn
    FROM stock_price_history
    WHERE reason = 'TopPlay'
),
ev AS (
    SELECT stock_id, created_at,
           (payload->>'NewTopScorePp')::numeric AS pp,
           row_number() OVER (PARTITION BY stock_id, created_at
                              ORDER BY (payload->>'NewTopScorePp')::numeric) AS rn
    FROM market_events
    WHERE event_type = 'TopPlayDetected'
)
SELECT h.id,
       e.pp AS play_pp,
       pp_lat.current_pp AS player_pp
FROM hist h
LEFT JOIN ev e
       ON e.stock_id = h.stock_id AND e.created_at = h.created_at AND e.rn = h.rn
LEFT JOIN player_stocks pst ON pst.id = h.stock_id
LEFT JOIN LATERAL (
    SELECT snap.current_pp
    FROM player_snapshots snap
    WHERE snap.tracked_player_id = pst.tracked_player_id
      AND snap.captured_at <= h.created_at
    ORDER BY snap.captured_at DESC
    LIMIT 1
) pp_lat ON true;

DO $$
DECLARE
    s uuid;
    h RECORD;
    running numeric;
    f numeric;
    pct numeric;
    newprice numeric;
    first_row boolean;
BEGIN
    FOR s IN SELECT DISTINCT stock_id FROM stock_price_history LOOP
        first_row := true;
        running := 0;

        FOR h IN
            SELECT sph.id, sph.reason, sph.previous_price, sph.new_price,
                   tpm.play_pp, tpm.player_pp
            FROM stock_price_history sph
            LEFT JOIN tp_map tpm ON tpm.id = sph.id
            WHERE sph.stock_id = s
            ORDER BY sph.created_at, sph.previous_price
        LOOP
            IF first_row THEN
                running := h.previous_price;   -- anchor: keep original starting price
                first_row := false;
            END IF;

            IF h.reason = 'TopPlay' THEN
                IF h.play_pp IS NOT NULL AND h.player_pp IS NOT NULL
                   AND h.player_pp > 0 AND h.play_pp > 0 THEN
                    pct := 0.6 * (h.play_pp / h.player_pp);
                    pct := GREATEST(0.005, LEAST(0.10, pct));
                ELSE
                    pct := 0.005;   -- fallback when pp data is missing
                END IF;
                f := 1 + pct;
            ELSE
                IF h.previous_price = 0 THEN
                    f := 1;
                ELSE
                    f := h.new_price / h.previous_price;   -- preserve original factor
                END IF;
            END IF;

            newprice := round(running * f, 4);
            IF newprice < 1 THEN newprice := 1; END IF;

            UPDATE stock_price_history
               SET previous_price = running, new_price = newprice
             WHERE id = h.id;

            running := newprice;
        END LOOP;

        IF NOT first_row THEN
            UPDATE player_stocks SET current_price = running WHERE id = s;
        END IF;
    END LOOP;
END $$;

COMMIT;
