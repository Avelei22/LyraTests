// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "Subsystems/GameInstanceSubsystem.h"
#include "TimerManager.h"
#include "LyraTestSupportSubsystem.generated.h"

UCLASS(meta = (DisplayName = "Lyra Test Support"))
class LYRAGAME_API ULyraTestSupportSubsystem : public UGameInstanceSubsystem
{
	GENERATED_BODY()

public:
	UFUNCTION(BlueprintCallable, Category = "Test|Automation")
	void SetLocalPlayerLookAtWorldPosition(float TargetX, float TargetY, float TargetZ);

	UFUNCTION(BlueprintCallable, Category = "Test|Automation")
	void SimulatePrimaryFire();

	UFUNCTION(BlueprintCallable, Category = "Test|Automation")
	void SetContinuousAimFireEnabled(bool bEnabled);

	UFUNCTION(BlueprintCallable, Category = "Test|Automation")
	void SetLocalPlayerInvincible(bool bEnable);

	UFUNCTION(BlueprintCallable, Category = "Test|Automation")
	void SetLocalPlayerInfiniteAmmo(bool bEnable);

	void TickContinuousAimFire();

private:
	FTimerHandle ContinuousAimFireHandle;
	TWeakObjectPtr<class ULyraTestSupportAimTickComponent> ContinuousAimTickComponent;
};
