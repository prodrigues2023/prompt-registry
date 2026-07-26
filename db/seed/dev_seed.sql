-- Development sample data — NOT applied by migrations, loaded on demand with `make seed`.
-- Two namespaces (checkout, search), two services each, every service a prompt with a version
-- history, gate results, and dev/staging/production aliases — enough to populate the screens and
-- exercise the filters. Prompt names follow the namespace.service convention.
--
-- Idempotent: it clears and reloads only these four seeded prompts, so it is safe to re-run and
-- never touches prompts you created yourself.

begin;

delete from alias_history where name in
  ('checkout.order-summary','checkout.refund-explainer','search.reranker','search.query-rewriter');
delete from aliases where name in
  ('checkout.order-summary','checkout.refund-explainer','search.reranker','search.query-rewriter');
delete from prompt_versions where name in
  ('checkout.order-summary','checkout.refund-explainer','search.reranker','search.query-rewriter');

-- checkout.order-summary --------------------------------------------------------------------
insert into prompt_versions (name, version, template, variables, metadata, content_hash, gate_status, test_results, created_at) values
 ('checkout.order-summary', 1, 'Summarise order {{order_id}} for {{customer}}.',
    '["order_id","customer"]', '{"author":"paulo","team":"checkout"}', 'sha256:1a2b3c4d5e6f7081', 'passed', '{"win_rate":0.86}', now() - interval '30 days'),
 ('checkout.order-summary', 2, 'Summarise order {{order_id}} for {{customer}}, including the items and total.',
    '["order_id","customer"]', '{"author":"paulo","team":"checkout"}', 'sha256:2b3c4d5e6f708192', 'passed', '{"win_rate":0.90}', now() - interval '21 days'),
 ('checkout.order-summary', 3, 'TL;DR the latest order for {{customer}}.',
    '["customer"]', '{"author":"paulo","team":"checkout"}', 'sha256:3c4d5e6f70819203', 'failed', '{"win_rate":0.68}', now() - interval '9 days'),
 ('checkout.order-summary', 4, 'Summarise order {{order_id}} for {{customer}} with items, total, and delivery ETA.',
    '["order_id","customer"]', '{"author":"paulo","team":"checkout"}', 'sha256:4d5e6f7081920314', 'passed', '{"win_rate":0.93}', now() - interval '6 days');

-- checkout.refund-explainer -----------------------------------------------------------------
insert into prompt_versions (name, version, template, variables, metadata, content_hash, gate_status, test_results, created_at) values
 ('checkout.refund-explainer', 1, 'Explain the refund for order {{order_id}}.',
    '["order_id"]', '{"author":"paulo","team":"checkout"}', 'sha256:5e6f708192031425', 'passed', '{"win_rate":0.82}', now() - interval '28 days'),
 ('checkout.refund-explainer', 2, 'Explain the refund for order {{order_id}}, with the amount and timeline.',
    '["order_id"]', '{"author":"paulo","team":"checkout"}', 'sha256:6f70819203142536', 'passed', '{"win_rate":0.88}', now() - interval '17 days'),
 ('checkout.refund-explainer', 3, 'Explain our refund policy to {{customer}}.',
    '["customer"]', '{"author":"paulo","team":"checkout"}', 'sha256:708192031425364a', 'failed', '{"win_rate":0.70}', now() - interval '8 days'),
 ('checkout.refund-explainer', 4, 'Explain the refund for order {{order_id}} with amount, method, and timeline.',
    '["order_id"]', '{"author":"paulo","team":"checkout"}', 'sha256:8192031425364a5b', 'passed', '{"win_rate":0.91}', now() - interval '4 days');

-- search.reranker ---------------------------------------------------------------------------
insert into prompt_versions (name, version, template, variables, metadata, content_hash, gate_status, test_results, created_at) values
 ('search.reranker', 1, 'Rank the search results for {{query}} by relevance.',
    '["query"]', '{"author":"paulo","team":"search"}', 'sha256:92031425364a5b6c', 'passed', '{"win_rate":0.79}', now() - interval '25 days'),
 ('search.reranker', 2, 'Rank the search results for {{query}} by relevance and recency.',
    '["query"]', '{"author":"paulo","team":"search"}', 'sha256:031425364a5b6c7d', 'passed', '{"win_rate":0.85}', now() - interval '14 days'),
 ('search.reranker', 3, 'Reorder the results for {{query}}.',
    '["query"]', '{"author":"paulo","team":"search"}', 'sha256:1425364a5b6c7d8e', 'untested', null, now() - interval '7 days'),
 ('search.reranker', 4, 'Rank the results for {{query}} by relevance, recency, and locale {{locale}}.',
    '["query","locale"]', '{"author":"paulo","team":"search"}', 'sha256:25364a5b6c7d8e9f', 'passed', '{"win_rate":0.90}', now() - interval '3 days');

-- search.query-rewriter ---------------------------------------------------------------------
insert into prompt_versions (name, version, template, variables, metadata, content_hash, gate_status, test_results, created_at) values
 ('search.query-rewriter', 1, 'Rewrite the query {{query}} to improve recall.',
    '["query"]', '{"author":"paulo","team":"search"}', 'sha256:364a5b6c7d8e9f01', 'passed', '{"win_rate":0.81}', now() - interval '26 days'),
 ('search.query-rewriter', 2, 'Rewrite {{query}} to improve recall without changing intent.',
    '["query"]', '{"author":"paulo","team":"search"}', 'sha256:4a5b6c7d8e9f0112', 'passed', '{"win_rate":0.87}', now() - interval '11 days'),
 ('search.query-rewriter', 3, 'Aggressively expand {{query}} with every synonym.',
    '["query"]', '{"author":"paulo","team":"search"}', 'sha256:5b6c7d8e9f011223', 'failed', '{"win_rate":0.66}', now() - interval '6 days'),
 ('search.query-rewriter', 4, 'Rewrite {{query}} to improve recall while preserving intent and entities.',
    '["query"]', '{"author":"paulo","team":"search"}', 'sha256:6c7d8e9f01122334', 'passed', '{"win_rate":0.92}', now() - interval '2 days');

-- Environment aliases: each service at a different point in its rollout, so the lanes differ. -
insert into aliases (name, environment, version) values
 ('checkout.order-summary',   'production', 2), ('checkout.order-summary',   'staging', 4), ('checkout.order-summary',   'dev', 4),
 ('checkout.refund-explainer','production', 2), ('checkout.refund-explainer','staging', 2), ('checkout.refund-explainer','dev', 4),
 ('search.reranker',          'production', 4), ('search.reranker',          'staging', 4), ('search.reranker',          'dev', 4),
 ('search.query-rewriter',    'production', 1), ('search.query-rewriter',    'staging', 2), ('search.query-rewriter',    'dev', 4);

-- A little history so version-history and rollback views have something to show. -------------
insert into alias_history (name, environment, from_version, to_version, action, changed_at) values
 ('checkout.order-summary',   'production', null, 1, 'promote', now() - interval '28 days'),
 ('checkout.order-summary',   'production', 1,    2, 'promote', now() - interval '20 days'),
 ('checkout.order-summary',   'production', 2,    3, 'promote', now() - interval '2 days 4 hours'),
 ('checkout.order-summary',   'production', 3,    2, 'rollback', now() - interval '2 days 2 hours'),
 ('checkout.order-summary',   'staging',    null, 2, 'promote', now() - interval '18 days'),
 ('checkout.order-summary',   'staging',    2,    4, 'promote', now() - interval '6 days'),
 ('checkout.order-summary',   'dev',        null, 4, 'promote', now() - interval '5 days'),
 ('checkout.refund-explainer','production', null, 1, 'promote', now() - interval '26 days'),
 ('checkout.refund-explainer','production', 1,    2, 'promote', now() - interval '16 days'),
 ('checkout.refund-explainer','dev',        null, 2, 'promote', now() - interval '15 days'),
 ('checkout.refund-explainer','dev',        2,    4, 'promote', now() - interval '4 days'),
 ('search.reranker',          'production', null, 2, 'promote', now() - interval '22 days'),
 ('search.reranker',          'production', 2,    4, 'promote', now() - interval '3 days'),
 ('search.query-rewriter',    'production', null, 1, 'promote', now() - interval '24 days'),
 ('search.query-rewriter',    'staging',    1,    2, 'promote', now() - interval '10 days'),
 ('search.query-rewriter',    'dev',        2,    4, 'promote', now() - interval '2 days');

commit;
