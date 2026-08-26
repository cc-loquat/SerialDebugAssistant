import subprocess, os, sys, tempfile

os.chdir(r"C:\Users\28244\Desktop\串口调试助手开发\SerialDebugAssistant")

def run(cmd, input_data=None):
    result = subprocess.run(cmd, shell=True, capture_output=True, text=True,
                            encoding='utf-8', errors='ignore', input=input_data)
    return result.stdout.strip(), result.stderr.strip(), result.returncode

def get_commit_info(commit):
    tree = run(f'git rev-parse {commit}^{{tree}}')[0]
    parents = run(f'git rev-list --parents -n 1 {commit}')[0].split()
    parents = parents[1:] if len(parents) > 1 else []
    msg = run(f'git log -1 --format=%B {commit}')[0]
    author_raw = run(f'git log -1 --format="%an <%ae>" {commit}')[0]
    committer_raw = run(f'git log -1 --format="%cn <%ce>" {commit}')[0]
    author_date = run(f'git log -1 --format="%ai" {commit}')[0]
    committer_date = run(f'git log -1 --format="%ci" {commit}')[0]
    return tree, parents, msg, author_raw, committer_raw, author_date, committer_date

def create_tree_without_claude(tree):
    """创建一个新的 tree，不包含 .claude 目录下的任何文件"""
    stdout, _, _ = run(f'git ls-tree -r {tree}')
    new_entries = []
    for line in stdout.split('\n'):
        if not line.strip():
            continue
        # 格式: <mode> <type> <sha>\t<path>
        if '\t' in line:
            meta, path = line.split('\t', 1)
            if path.startswith('.claude/'):
                continue
            new_entries.append(line)
        else:
            # 根目录 .claude (非 blob，可能是 tree)
            continue
    
    if not new_entries:
        # 空的 tree
        return run('git hash-object -t tree /dev/null 2>&1 || echo "4b825dc642cb6eb9a060e54bf8d69288fbee4904"')[0]
    
    tree_data = '\n'.join(new_entries) + '\n'
    # 使用 git mktree
    stdout, _, _ = run(f'git mktree', input_data=tree_data)
    if stdout:
        return stdout.strip().split('\n')[0].strip()
    else:
        return None

def create_commit(tree, parents, msg, author, committer, author_date, committer_date):
    commit_data = f"tree {tree}\n"
    for p in parents:
        commit_data += f"parent {p}\n"
    commit_data += f"author {author} {author_date}\n"
    commit_data += f"committer {committer} {committer_date}\n"
    commit_data += f"\n{msg}\n"
    
    stdout, _, _ = run(f'git hash-object -t commit --stdin -w', input_data=commit_data)
    return stdout.strip().split('\n')[0].strip()

# 获取所有提交，从 oldest 到 newest
stdout, _, _ = run('git rev-list --all --reverse')
all_commits = [c.strip() for c in stdout.split('\n') if len(c.strip()) == 40]

print(f"Total commits: {len(all_commits)}")

# 查找包含 .claude 的提交
claude_commits = set()
for c in all_commits:
    stdout, _, _ = run(f'git ls-tree -r {c} --name-only')
    if '.claude' in stdout:
        claude_commits.add(c)

print(f"Commits containing .claude in tree: {claude_commits}")

# 重写提交
commit_map = {}
new_head = None
for old_commit in all_commits:
    tree, parents, msg, author, committer, ad, cd = get_commit_info(old_commit)
    new_parents = [commit_map.get(p, p) for p in parents]
    
    # 检查 tree 中是否包含 .claude
    stdout, _, _ = run(f'git ls-tree -r {tree} --name-only')
    has_claude = '.claude' in stdout
    
    if has_claude:
        new_tree = create_tree_without_claude(tree)
        if not new_tree or new_tree == '':
            print(f"ERROR: failed to create new tree for {old_commit}")
            sys.exit(1)
        
        # 检查新的 tree 是否和某个 parent 相同（空提交）
        skip = False
        if new_parents:
            parent_tree = run(f'git rev-parse {new_parents[0]}^{{tree}}')[0]
            if parent_tree == new_tree:
                skip = True
        
        if skip:
            print(f"Skipping empty commit {old_commit[:8]} (tree same as parent)")
            commit_map[old_commit] = new_parents[0]
            new_head = commit_map[old_commit]
            continue
        
        new_commit = create_commit(new_tree, new_parents, msg, author, committer, ad, cd)
        print(f"Rewrote {old_commit[:8]} -> {new_commit[:8]} (removed .claude)")
        commit_map[old_commit] = new_commit
        new_head = new_commit
    else:
        if new_parents != parents:
            # parent 变了，需要重写
            new_commit = create_commit(tree, new_parents, msg, author, committer, ad, cd)
            print(f"Rewrote {old_commit[:8]} -> {new_commit[:8]} (parent updated)")
            commit_map[old_commit] = new_commit
            new_head = new_commit
        else:
            commit_map[old_commit] = old_commit
            new_head = old_commit

print(f"\nNew HEAD: {new_head}")
print(f"Old HEAD: {run('git rev-parse HEAD')[0]}")

# 更新 main 分支
if new_head:
    run(f'git update-ref refs/heads/main {new_head}')
    print("Updated refs/heads/main")
    
    # 清理
    run('git reflog expire --expire=now --all')
    run('git gc --prune=now --aggressive')
    print("Done.")
