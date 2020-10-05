#!/usr/bin/env bash

EmulatorStoragePath=obj/azurite
EmulatorLogPath=obj/azurite.log

if [ ! -d "$EmulatorStoragePath" ]
then
    mkdir $EmulatorStoragePath
fi

yarn run azurite -l $EmulatorStoragePath -d $EmulatorLogPath
