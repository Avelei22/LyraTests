// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "Components/ActorComponent.h"
#include "LyraTestSupportAimTickComponent.generated.h"

class ULyraTestSupportSubsystem;

UCLASS(meta = (DisplayName = "Lyra Test Support Aim Tick"), Category = "Test|Automation", Transient)
class LYRAGAME_API ULyraTestSupportAimTickComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	ULyraTestSupportAimTickComponent(const FObjectInitializer& ObjectInitializer);

	void SetSubsystem(ULyraTestSupportSubsystem* InSubsystem) { TestSupportSubsystem = InSubsystem; }

protected:
	virtual void TickComponent(float DeltaTime, enum ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

	UPROPERTY()
	TWeakObjectPtr<ULyraTestSupportSubsystem> TestSupportSubsystem;
};
