git submodule update --recursive --remote
git submodule foreach git pull origin master

git push origin master
git submodule foreach git push origin master
