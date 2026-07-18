# Evidence Index

This folder is generated from real git history. Use it as source material for truthful portfolio notes, blog drafts, or immigration-supporting documentation. Do not alter dates or claims to imply work happened at a different time.

## Files

- `commits.csv`: one row per commit with date, hash, author, and subject.
- `file-changes.csv`: one row per changed file per commit, including additions/deletions.
- `repo-activity-summary.md`: aggregate counts by date and area.
- `medium-drafts/`: review-gated article drafts built from real commit periods.
- `published-posts.csv`: created by the publisher after a successful Medium API post.

## Review-Gated Publishing

Generated drafts start with:

```yaml
approved: false
publish: false
```

Only set both values to `true` after reviewing the article. The publisher script refuses to post drafts that are not explicitly approved. It also requires `MEDIUM_INTEGRATION_TOKEN` in the environment and logs successful posts to `published-posts.csv`.

## Suggested Evidence Packet

1. Export or screenshot the repository commit history from the remote host, if available.
2. Keep commit hashes visible so the activity can be verified.
3. Publish articles with the current publication date, and describe older dates as "work covered" or "development period."
4. If importing an article that was genuinely published elsewhere first, keep the canonical link to the original.
