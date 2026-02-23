// Copyright Epic Games, Inc. All Rights Reserved.

#include "Testing/LyraTestSupportAimTickComponent.h"
#include "Testing/LyraTestSupportSubsystem.h"
#include "Engine/World.h"
#include "Engine/GameInstance.h"

#include UE_INLINE_GENERATED_CPP_BY_NAME(LyraTestSupportAimTickComponent)

ULyraTestSupportAimTickComponent::ULyraTestSupportAimTickComponent(const FObjectInitializer& ObjectInitializer)
	: Super(ObjectInitializer)
{
	PrimaryComponentTick.TickGroup = TG_LastDemotable;
	PrimaryComponentTick.bCanEverTick = true;
	PrimaryComponentTick.bAllowTickOnDedicatedServer = false;
	PrimaryComponentTick.bStartWithTickEnabled = true;
}

void ULyraTestSupportAimTickComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);
	if (ULyraTestSupportSubsystem* Sub = TestSupportSubsystem.Get())
	{
		Sub->TickContinuousAimFire();
	}
}
