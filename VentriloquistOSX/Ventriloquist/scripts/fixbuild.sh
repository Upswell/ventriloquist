#!/bin/sh
set -e
ENVIRONMENT="debug"
BUILD_PATH="../bin/Debug/"
 
function usage()
{
    echo "The MonoMac bundler misses some things, this will fix that.  Run with debug or release (--environment=debug)"
    echo ""
    echo "./fixbuild.sh"
    #echo "\t-h --help"
    echo "\t--environment=$ENVIRONMENT"
    #echo "\t--db-path=$DB_PATH"
    echo ""
}
 
while [ "$1" != "" ]; do
    PARAM=`echo $1 | awk -F= '{print $1}'`
    VALUE=`echo $1 | awk -F= '{print $2}'`
    case $PARAM in
        -h | --help)
            usage
            exit
            ;;
        --environment)
            ENVIRONMENT=$VALUE
            ;;
        --db-path)
            DB_PATH=$VALUE
            ;;
        *)
            echo "ERROR: unknown parameter \"$PARAM\""
            usage
            exit 1
            ;;
    esac
    shift
done
 
 
#echo "ENVIRONMENT is $ENVIRONMENT";
#echo "DB_PATH is $DB_PATH";

if [ "$ENVIRONMENT" = "debug" ]
then
  echo "Fixing Debug build"
  BUILD_PATH="../bin/Debug/"
elif [ "$ENVIRONMENT" = "release" ]
then
  echo "Fixing Release build"
  BUILD_PATH="../bin/Release/"
else 
  echo "invalid environment"
  exit 0
fi

cd "$BUILD_PATH"
cp log4net.config Ventriloquist.app/Contents/MonoBundle/log4net.config
cp xamspeech.dylib Ventriloquist.app/Contents/MonoBundle/xamspeech.dylib
cp -R content Ventriloquist.app/Contents/MonoBundle/
cp -R Views Ventriloquist.app/Contents/MonoBundle/
cp -R log Ventriloquist.app/Contents/MonoBundle/
cd ../../scripts