// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "Kismet/BlueprintFunctionLibrary.h"
#include "LyraTestEnemyQuery.generated.h"

UCLASS(meta = (DisplayName = "Lyra Test Enemy Query"))
class LYRAGAME_API ULyraTestEnemyQuery : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category = "Test|Automation", meta = (WorldContext = "WorldContextObject"))
	static FString GetEnemyLocationsAsString(UObject* WorldContextObject, int32 PlayerIndex = 0);

	UFUNCTION(BlueprintCallable, Category = "Test|Automation", meta = (WorldContext = "WorldContextObject"))
	static FString GetTestPositionsAsString(UObject* WorldContextObject, int32 PlayerIndex = 0);

	UFUNCTION(BlueprintCallable, Category = "Test|Automation", meta = (WorldContext = "WorldContextObject"))
	static FString GetEnemyOnlyTestPositionsAsString(UObject* WorldContextObject, int32 PlayerIndex = 0);

	UFUNCTION(BlueprintCallable, Category = "Test|Automation", meta = (WorldContext = "WorldContextObject"))
	static FString GetEnemyOnlyPositionsAndAimAt(UObject* WorldContextObject, int32 PlayerIndex, float TargetX, float TargetY, float TargetZ, bool bFire = false);

	UFUNCTION(BlueprintCallable, Category = "Test|Automation", meta = (WorldContext = "WorldContextObject"))
	static void SetLocalPlayerInvincible(UObject* WorldContextObject, bool bEnable);

	UFUNCTION(BlueprintCallable, Category = "Test|Automation", meta = (WorldContext = "WorldContextObject"))
	static void SetLocalPlayerInfiniteAmmo(UObject* WorldContextObject, bool bEnable);

	UFUNCTION(BlueprintCallable, Category = "Test|Automation", meta = (WorldContext = "WorldContextObject"))
	static int64 GetLocalPlayerPawnId(UObject* WorldContextObject, int32 PlayerIndex = 0);
};
