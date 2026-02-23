// Copyright Epic Games, Inc. All Rights Reserved.

#include "Testing/LyraTestSupportSubsystem.h"
#include "Testing/LyraTestEnemyQuery.h"
#include "Testing/LyraTestSupportAimTickComponent.h"
#include "Engine/EngineTypes.h"
#include "Engine/GameInstance.h"
#include "Engine/World.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/PlayerInput.h"
#include "GameFramework/Pawn.h"
#include "Camera/PlayerCameraManager.h"
#include "Math/UnrealMathUtility.h"
#include "InputCoreTypes.h"
#include "Player/LyraPlayerController.h"
#include "AbilitySystem/LyraAbilitySystemComponent.h"
#include "Character/LyraPawnExtensionComponent.h"
#include "Character/LyraPawnData.h"
#include "Input/LyraInputConfig.h"
#include "LyraGameplayTags.h"
#include "GameplayTagsManager.h"
#include "EnhancedInputSubsystems.h"
#include "InputAction.h"
#include "Equipment/LyraEquipmentManagerComponent.h"
#include "Weapons/LyraRangedWeaponInstance.h"
#include "Inventory/LyraInventoryItemInstance.h"

#include UE_INLINE_GENERATED_CPP_BY_NAME(LyraTestSupportSubsystem)

static const FName TagName_MagazineAmmo(TEXT("Lyra.ShooterGame.Weapon.MagazineAmmo"));
static const FName TagName_SpareAmmo(TEXT("Lyra.ShooterGame.Weapon.SpareAmmo"));
static constexpr int32 InfiniteAmmoStackCount = 99999;

static const FName WeaponFireInputTagNames[] = {
	FName(TEXT("InputTag.Weapon.Fire")),
	FName(TEXT("InputTag.Weapon.FireAuto")),
	FName(TEXT("InputTag.Weapon.Primary"))
};

static constexpr float ViewPitchMinDeg = -89.f;
static constexpr float ViewPitchMaxDeg = 89.f;

void ULyraTestSupportSubsystem::SetLocalPlayerLookAtWorldPosition(float TargetX, float TargetY, float TargetZ)
{
	UGameInstance* GI = GetGameInstance();
	if (!GI || !GI->GetWorld())
	{
		return;
	}

	APlayerController* PC = GI->GetFirstLocalPlayerController();
	if (!PC)
	{
		return;
	}

	FVector ViewLocation;
	FRotator ViewRotation;
	if (PC->PlayerCameraManager)
	{
		PC->PlayerCameraManager->GetCameraViewPoint(ViewLocation, ViewRotation);
	}
	else
	{
		PC->GetPlayerViewPoint(/*out*/ ViewLocation, /*out*/ ViewRotation);
	}

	const FVector Target(TargetX, TargetY, TargetZ);
	FVector Dir = (Target - ViewLocation).GetSafeNormal();
	if (Dir.IsNearlyZero())
	{
		return;
	}

	float YawRad = FMath::Atan2(Dir.Y, Dir.X);
	float PitchRad = FMath::Asin(FMath::Clamp(Dir.Z, -1.f, 1.f));
	float PitchDeg = FMath::RadiansToDegrees(PitchRad);
	float YawDeg = FMath::RadiansToDegrees(YawRad);
	PitchDeg = FMath::Clamp(PitchDeg, ViewPitchMinDeg, ViewPitchMaxDeg);

	const FRotator NewRotation(PitchDeg, YawDeg, 0.f);
	PC->SetControlRotation(NewRotation);
}

void ULyraTestSupportSubsystem::SimulatePrimaryFire()
{
	UGameInstance* GI = GetGameInstance();
	if (!GI) return;
	APlayerController* PC = GI->GetFirstLocalPlayerController();
	if (!PC) return;
	UWorld* World = GI->GetWorld();
	const float DeltaTime = World ? World->GetDeltaSeconds() : 0.016f;

	APawn* Pawn = PC->GetPawn();
	if (Pawn)
	{
		if (ULyraPawnExtensionComponent* PawnExt = ULyraPawnExtensionComponent::FindPawnExtensionComponent(Pawn))
		{
			if (const ULyraPawnData* PawnData = PawnExt->GetPawnData<ULyraPawnData>())
			{
				if (const ULyraInputConfig* InputConfig = PawnData->InputConfig)
				{
					ULocalPlayer* LP = PC->GetLocalPlayer();
					if (UEnhancedInputLocalPlayerSubsystem* Subsystem = LP ? LP->GetSubsystem<UEnhancedInputLocalPlayerSubsystem>() : nullptr)
					{
						TArray<UInputModifier*> Modifiers;
						TArray<UInputTrigger*> Triggers;
						for (FName TagName : WeaponFireInputTagNames)
						{
							FGameplayTag FireTag = UGameplayTagsManager::Get().RequestGameplayTag(TagName, false);
							if (FireTag.IsValid())
							{
								if (const UInputAction* FireAction = InputConfig->FindAbilityInputActionForTag(FireTag, false))
								{
									Subsystem->InjectInputVectorForAction(FireAction, FVector(1.0, 0.0, 0.0), Modifiers, Triggers);
									TWeakObjectPtr<UEnhancedInputLocalPlayerSubsystem> SubsystemWeak(Subsystem);
									const UInputAction* ActionToRelease = FireAction;
									FTimerHandle ReleaseHandle;
									World->GetTimerManager().SetTimer(ReleaseHandle, [SubsystemWeak, ActionToRelease]()
									{
										if (SubsystemWeak.IsValid())
										{
											TArray<UInputModifier*> M;
											TArray<UInputTrigger*> T;
											SubsystemWeak->InjectInputVectorForAction(ActionToRelease, FVector(0.0, 0.0, 0.0), M, T);
										}
									}, 0.05f, false);
									return;
								}
							}
						}
					}
				}
			}
		}
	}

	ULyraAbilitySystemComponent* ASC = nullptr;
	if (ALyraPlayerController* LyraPC = Cast<ALyraPlayerController>(PC))
	{
		ASC = LyraPC->GetLyraAbilitySystemComponent();
	}
	if (!ASC && Pawn)
	{
		if (ULyraPawnExtensionComponent* PawnExt = ULyraPawnExtensionComponent::FindPawnExtensionComponent(Pawn))
		{
			ASC = PawnExt->GetLyraAbilitySystemComponent();
		}
	}
	if (ASC)
	{
		const UGameplayTagsManager& TagManager = UGameplayTagsManager::Get();
		for (FName TagName : WeaponFireInputTagNames)
		{
			FGameplayTag FireTag = TagManager.RequestGameplayTag(TagName, false);
			if (FireTag.IsValid())
			{
				ASC->AbilityInputTagPressed(FireTag);
				ASC->ProcessAbilityInput(DeltaTime, false);
				TWeakObjectPtr<ULyraAbilitySystemComponent> ASCWeak(ASC);
				FTimerHandle ReleaseHandle;
				World->GetTimerManager().SetTimer(ReleaseHandle, [ASCWeak, FireTag]()
				{
					if (ULyraAbilitySystemComponent* Ptr = ASCWeak.Get())
					{
						Ptr->AbilityInputTagReleased(FireTag);
						Ptr->ProcessAbilityInput(0.016f, false);
					}
				}, 0.05f, false);
				return;
			}
		}
	}

	PC->InputKey(FInputKeyParams(EKeys::LeftMouseButton, EInputEvent::IE_Pressed, FVector::ZeroVector, false, FInputDeviceId()));
	PC->InputKey(FInputKeyParams(EKeys::LeftMouseButton, EInputEvent::IE_Released, FVector::ZeroVector, false, FInputDeviceId()));
}

void ULyraTestSupportSubsystem::SetContinuousAimFireEnabled(bool bEnabled)
{
	UGameInstance* GI = GetGameInstance();
	if (!GI) return;

	if (ULyraTestSupportAimTickComponent* Existing = ContinuousAimTickComponent.Get())
	{
		Existing->DestroyComponent();
		ContinuousAimTickComponent.Reset();
	}
	if (ContinuousAimFireHandle.IsValid())
	{
		GI->GetTimerManager().ClearTimer(ContinuousAimFireHandle);
		ContinuousAimFireHandle.Invalidate();
	}

	if (bEnabled)
	{
		APlayerController* PC = GI->GetFirstLocalPlayerController();
		if (PC)
		{
			ULyraTestSupportAimTickComponent* Comp = NewObject<ULyraTestSupportAimTickComponent>(PC, ULyraTestSupportAimTickComponent::StaticClass(), NAME_None, RF_Transient);
			if (Comp)
			{
				Comp->SetSubsystem(this);
				Comp->RegisterComponent();
				ContinuousAimTickComponent = Comp;
			}
		}
		if (!ContinuousAimTickComponent.IsValid())
		{
			GI->GetTimerManager().SetTimer(ContinuousAimFireHandle, this, &ULyraTestSupportSubsystem::TickContinuousAimFire, 0.f, true);
		}
	}
}

void ULyraTestSupportSubsystem::TickContinuousAimFire()
{
	UGameInstance* GI = GetGameInstance();
	if (!GI) return;
	UWorld* World = GI->GetWorld();
	if (!World) return;

	const FString Data = ULyraTestEnemyQuery::GetEnemyOnlyTestPositionsAsString(World, 0);
	if (Data.IsEmpty()) return;

	FVector PlayerPos(0.f, 0.f, 0.f);
	TArray<FVector> EnemyPositions;
	TArray<FString> Parts;
	Data.ParseIntoArray(Parts, TEXT("|"), true);
	for (const FString& Part : Parts)
	{
		FString Trimmed = Part.TrimStartAndEnd();
		if (Trimmed.Len() < 5) continue;
		TArray<FString> Tokens;
		Trimmed.ParseIntoArray(Tokens, TEXT(","), true);
		if (Tokens.Num() < 4) continue;
		float X = 0.f, Y = 0.f, Z = 0.f;
		if (!LexTryParseString(X, *Tokens[1]) || !LexTryParseString(Y, *Tokens[2]) || !LexTryParseString(Z, *Tokens[3])) continue;
		if (Trimmed.StartsWith(TEXT("P,"), ESearchCase::IgnoreCase))
			PlayerPos = FVector(X, Y, Z);
		else if (Trimmed.StartsWith(TEXT("E,"), ESearchCase::IgnoreCase))
			EnemyPositions.Add(FVector(X, Y, Z));
	}

	if (EnemyPositions.Num() == 0) return;

	float BestDistSq = FLT_MAX;
	FVector Best(0.f, 0.f, 0.f);
	for (const FVector& E : EnemyPositions)
	{
		float D = (E - PlayerPos).SizeSquared();
		if (D < BestDistSq) { BestDistSq = D; Best = E; }
	}

	constexpr float AimZOffset = 40.f;
	SetLocalPlayerLookAtWorldPosition(Best.X, Best.Y, Best.Z + AimZOffset);
	SimulatePrimaryFire();
}

void ULyraTestSupportSubsystem::SetLocalPlayerInvincible(bool bEnable)
{
	UGameInstance* GI = GetGameInstance();
	if (!GI) return;
	APlayerController* PC = GI->GetFirstLocalPlayerController();
	if (!PC) return;

	ULyraAbilitySystemComponent* ASC = nullptr;
	if (ALyraPlayerController* LyraPC = Cast<ALyraPlayerController>(PC))
	{
		ASC = LyraPC->GetLyraAbilitySystemComponent();
	}
	if (!ASC && PC->GetPawn())
	{
		if (ULyraPawnExtensionComponent* PawnExt = ULyraPawnExtensionComponent::FindPawnExtensionComponent(PC->GetPawn()))
		{
			ASC = PawnExt->GetLyraAbilitySystemComponent();
		}
	}
	if (!ASC) return;

	const FGameplayTag Tag = LyraGameplayTags::Cheat_GodMode;
	if (bEnable)
		ASC->AddDynamicTagGameplayEffect(Tag);
	else
		ASC->RemoveDynamicTagGameplayEffect(Tag);
}

void ULyraTestSupportSubsystem::SetLocalPlayerInfiniteAmmo(bool bEnable)
{
	if (!bEnable) return;
	UGameInstance* GI = GetGameInstance();
	if (!GI) return;
	APlayerController* PC = GI->GetFirstLocalPlayerController();
	if (!PC) return;
	APawn* Pawn = PC->GetPawn();
	if (!Pawn) return;

	if (Pawn->GetLocalRole() != ROLE_Authority)
	{
		return;
	}

	ULyraEquipmentManagerComponent* EquipmentManager = Pawn->FindComponentByClass<ULyraEquipmentManagerComponent>();
	if (!EquipmentManager) return;

	const UGameplayTagsManager& TagManager = UGameplayTagsManager::Get();
	const FGameplayTag MagazineTag = TagManager.RequestGameplayTag(TagName_MagazineAmmo, false);
	const FGameplayTag SpareTag = TagManager.RequestGameplayTag(TagName_SpareAmmo, false);
	if (!MagazineTag.IsValid() && !SpareTag.IsValid()) return;

	const TArray<ULyraEquipmentInstance*> RangedWeapons = EquipmentManager->GetEquipmentInstancesOfType(ULyraRangedWeaponInstance::StaticClass());
	for (ULyraEquipmentInstance* EquipInstance : RangedWeapons)
	{
		ULyraInventoryItemInstance* ItemInstance = Cast<ULyraInventoryItemInstance>(EquipInstance->GetInstigator());
		if (!ItemInstance) continue;
		if (MagazineTag.IsValid())
			ItemInstance->AddStatTagStack(MagazineTag, InfiniteAmmoStackCount);
		if (SpareTag.IsValid())
			ItemInstance->AddStatTagStack(SpareTag, InfiniteAmmoStackCount);
	}
}
