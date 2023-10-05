git submodule update --recursive --remote

git push origin master
git submodule foreach git push origin master
