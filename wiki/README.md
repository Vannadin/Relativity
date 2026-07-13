# wiki/ — source for the GitHub wiki

These Markdown files are the **source of truth** for the project's GitHub wiki (usage + background
theory). They are version-controlled here (the wiki repo is easy to lose) and published to the separate
`Relativity.wiki.git` repository (the public repo's wiki; this README stays out of the push).

GitHub-wiki conventions used:
- `Home.md` is the landing page; `_Sidebar.md` / `_Footer.md` render on every page.
- Page filenames map to titles (`The-Physics.md` → "The Physics"); links use `[[Page Name]]`.

## Publishing (owner)

The wiki repo only exists once its first page is created. After enabling the wiki feature on the repo:

```sh
# one-time: initialize + push all pages
git clone https://github.com/Vannadin/Relativity.wiki.git /tmp/relativity-wiki   # if it exists
# (if the clone 404s, create any page once via the web UI "Create the first page", then clone)
cp wiki/*.md /tmp/relativity-wiki/
cd /tmp/relativity-wiki && git add -A && git commit -m "Sync wiki from main repo" && git push
```

Keep these files as the edit surface, then re-run the copy+push to update the live wiki.
